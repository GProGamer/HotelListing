﻿using HotelListing.API.Data.Configurations;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HotelListing.API.Data
{
    public class HotelListingDbContext : IdentityDbContext<ApiUser>
    {
        public HotelListingDbContext(DbContextOptions options) : base(options)
        {

        }

        public DbSet<Hotel> Hotels { get; set; }
        public DbSet<Country> Countries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new RoleConfiguration());
            modelBuilder.ApplyConfiguration(new CountryConfiguration());
            modelBuilder.ApplyConfiguration(new HotelConfiguration());
        }

        //public DbSet<Hotel> Hotels { get; set; }

        //public DbSet<Country> Countries { get; set; }

        //protected override void OnModelCreating(ModelBuilder modelBuilder)
        //{
        //    base.OnModelCreating(modelBuilder);

        //    modelBuilder.Entity<Country>().HasData(
        //        new Country
        //        {
        //            Id = 1,
        //            Name = "Jamaica",
        //            ShortName = "JM",

        //        },
        //        new Country
        //        {
        //            Id = 2,
        //            Name = "Peru",
        //            ShortName = "PE",

        //        },
        //        new Country
        //        {
        //            Id = 3,
        //            Name = "Colombia",
        //            ShortName = "CO",

        //        }
        //        );

        //    modelBuilder.Entity<Hotel>().HasData(

        //        new Hotel
        //        {
        //            Id = 1,
        //            Name = "Hotel numero1",
        //            Address = "direc1",
        //            CountryId = 1,
        //            Rating = 4.3
        //        },
        //        new Hotel
        //        {
        //            Id = 2,
        //            Name = "Hotel numero2",
        //            Address = "direc2",
        //            CountryId = 2,
        //            Rating = 4.7
        //        },
        //        new Hotel
        //        {
        //            Id = 3,
        //            Name = "Hotel numero3",
        //            Address = "direc3",
        //            CountryId = 3,
        //            Rating = 3.5
        //        }


        //        );
        //}

        public class HotelListingDbContextFactory : IDesignTimeDbContextFactory<HotelListingDbContext>
        {
            public HotelListingDbContext CreateDbContext(string[] args)
            {
                IConfiguration config = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                var optionsBuilder = new DbContextOptionsBuilder<HotelListingDbContext>();
                var conn = config.GetConnectionString("HotelListingDbConnectionString");
                optionsBuilder.UseSqlServer(conn);
                return new HotelListingDbContext(optionsBuilder.Options);
            }
        }
    }
}
