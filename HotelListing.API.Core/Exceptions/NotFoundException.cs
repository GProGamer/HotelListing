namespace HotelListing.API.Core.Exceptions
{
    public class NotFoundException : ApplicationException
    {
        public NotFoundException(string name, object key ) : base($"{name} and {key} was not found")
        {
            
        }
    }
}
