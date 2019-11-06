interface IRpcClient
{
    T Call<T>(object[] args);
}