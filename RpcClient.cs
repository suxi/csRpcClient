using System;
using System.Text;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Reflection;
using System.Threading.Tasks;

namespace csRpcClient
{
    internal struct Error
    {
        public string title { get; set; }
        public int code { get; set; }
        public string message { get; set; }
    }
    internal struct ErrorResp
    {
        public Error error { get; set; }
        public string id { get; set; }
    }
    internal struct Message
    {
        public string method { get; set; }
        public object[] @params { get; set; }
        public string id { get; set; }
    }
    internal partial class RpcClient : IRpcClient, IDisposable
    {
        private string DUMMY = "1";
        private readonly Type ProviderType = typeof(RpcClient);
        private readonly string QueueName = "inner.test.2";
        private readonly IConnection Conn;
        private readonly IModel srvChannel;
        private readonly BlockingCollection<byte[]> respQueue = new BlockingCollection<byte[]>();
        public RpcClient(IConfiguration config)
        {
            var factory = new ConnectionFactory()
            {
                HostName = config["rabbitmq:Host"],
                Port = int.Parse(config["rabbitmq:Port"]),
                UserName = config["rabbitmq:Username"],
                Password = config["rabbitmq:Password"]
            };
            Conn = factory.CreateConnection();
            srvChannel = Conn.CreateModel();

            InitConsumer();
        }

        private void InitConsumer()
        {
            srvChannel.QueueDeclare(queue: QueueName, exclusive: false, durable: false, autoDelete: true);
            srvChannel.BasicQos(0, 1, false);
            var consumer = new EventingBasicConsumer(srvChannel);
            srvChannel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);
            Console.WriteLine(" [x] Awaiting RPC requests");
            consumer.Received += (model, ea) =>
            {
                string response = null;
                var body = ea.Body;
                var props = ea.BasicProperties;
                var replyProps = srvChannel.CreateBasicProperties();
                replyProps.CorrelationId = props.CorrelationId;
                try
                {
                    var bodyString = Encoding.UTF8.GetString(body);
                    var message = JsonConvert.DeserializeObject<Message>(bodyString);
                    if (ProviderType.GetMethod(message.method) == null)
                    {
                        response = JsonConvert.SerializeObject(
                            new Error
                            {
                                title = "Method not found",
                                code = -32601,
                                message = "指令不存在或不可用"
                            }
                        );
                    }
                    else
                    {
                        var func = ProviderType.GetMethod(message.method);
                        var ret = func.Invoke(this, message.@params);
                        response = (string)ret;
                    }
                    Console.WriteLine(response);
                }
                catch (Exception e)
                {
                    response = JsonConvert.SerializeObject(
                        new ErrorResp
                        {
                            error = new Error
                            {
                                title = "Parse error",
                                code = -32700,
                                message = e.Message
                            },
                            id = "1"
                        }
                    );
                }
                finally
                {
                    var responseBytes = Encoding.UTF8.GetBytes(response);
                    srvChannel.BasicPublish(
                        exchange: "",
                        routingKey: props.ReplyTo,
                        basicProperties: replyProps,
                        body: responseBytes);
                    srvChannel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
            };
        }

        public Task<string> CallAsync(string queue, string method, int timeout = 5000, params object[] args)
        {
            var tcs = new TaskCompletionSource<string>();
            using (var callChannel = Conn.CreateModel())
            {
                try
                {
                    var topic_queue_check = callChannel.QueueDeclarePassive(queue: queue);
                    if (topic_queue_check.ConsumerCount <= 0)
                    {
                        //服务丢失
                        tcs.SetException(new Exception("Service missing"));
                        return tcs.Task;
                    }
                }
                catch (System.Exception)
                {
                    //服务丢失
                    tcs.SetException(new Exception("Service missing"));
                    return tcs.Task;
                }
                var replyQueueNmae = callChannel.QueueDeclare().QueueName;
                var props = callChannel.CreateBasicProperties();
                var correlationId = Guid.NewGuid().ToString();
                props.CorrelationId = correlationId;
                props.ReplyTo = replyQueueNmae;
                var consumer = new EventingBasicConsumer(callChannel);
                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body;
                    if (ea.BasicProperties.CorrelationId == correlationId)
                    {
                        tcs.SetResult(Encoding.UTF8.GetString(body));
                    }
                };
                var message = new Message
                {
                    method = method,
                    @params = args,
                    id = DUMMY
                };
                var messageBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
                callChannel.BasicPublish(
                    exchange: "",
                    routingKey: queue,
                    basicProperties: props,
                    body: messageBytes
                );
                callChannel.BasicConsume(consumer: consumer, queue: replyQueueNmae, autoAck: true);
                return tcs.Task;
            }
        }
        public string Call(string queue, string method, int timeout = 5000, params object[] args)
        {
            using (var callChannel = Conn.CreateModel())
            {
                try
                {
                    var topic_queue_check = callChannel.QueueDeclarePassive(queue: queue);
                    if (topic_queue_check.ConsumerCount <= 0)
                    {
                        //服务丢失
                        var response = JsonConvert.SerializeObject(
                            new ErrorResp
                            {
                                error = new Error
                                {
                                    title = "Service missing",
                                    code = -32604,
                                    message = $"{queue} 服务未开启"
                                },
                                id = "1"
                            }
                        );
                        return response;
                    }
                }
                catch (System.Exception)
                {
                    //服务丢失
                    var response = JsonConvert.SerializeObject(
                        new ErrorResp
                        {
                            error = new Error
                            {
                                title = "Service missing",
                                code = -32604,
                                message = $"{queue} 服务未开启"
                            },
                            id = DUMMY
                        }
                    );
                    return response;
                }
                var replyQueueNmae = callChannel.QueueDeclare().QueueName;
                var props = callChannel.CreateBasicProperties();
                var correlationId = Guid.NewGuid().ToString();
                props.CorrelationId = correlationId;
                props.ReplyTo = replyQueueNmae;
                var consumer = new EventingBasicConsumer(callChannel);
                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body;
                    if (ea.BasicProperties.CorrelationId == correlationId)
                    {
                        respQueue.Add(body);
                    }
                };
                var message = new Message
                {
                    method = method,
                    @params = args,
                    id = DUMMY
                };
                var messageBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
                callChannel.BasicPublish(
                    exchange: "",
                    routingKey: queue,
                    basicProperties: props,
                    body: messageBytes
                );
                callChannel.BasicConsume(consumer: consumer, queue: replyQueueNmae, autoAck: true);
                var ret = respQueue.TryTake(out byte[] responseBytes, timeout);
                if (ret)
                {
                    return Encoding.UTF8.GetString(responseBytes);
                }
                else
                {
                    return null;
                }
            }
        }

        public void Dispose()
        {
            Conn.Close();
        }
    }
}
