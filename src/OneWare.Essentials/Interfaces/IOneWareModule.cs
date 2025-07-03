namespace OneWare.Essentials.Interfaces
{
    public interface IOneWareModule
    {
        void RegisterTypes();
        void OnExecute();
    }
}