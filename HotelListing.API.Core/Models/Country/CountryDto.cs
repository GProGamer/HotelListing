﻿using HotelListing.API.Core.Models.Hotel;

namespace HotelListing.API.Core.Models.Country
{
    public class CountryDto : BaseCountryDto
    {
        public int Id { get; set; }

        public virtual IList<HotelDto> Hotels { get; set; }
    }
}
