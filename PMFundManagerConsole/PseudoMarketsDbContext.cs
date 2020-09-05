using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;
using PMCommonEntities.Models;
using PMUnifiedAPI.Models;

namespace PMFundManagerConsole
{
    public class PseudoMarketsDbContext : DbContext
    {
        public DbSet<PseudoFunds> PseudoFunds { get; set; }
        public DbSet<PseudoFundHistories> PseudoFundHistories { get; set; }
        public DbSet<PseudoFundUnderlyingSecurities> PseudoFundUnderlyingSecurities { get; set; }
        public DbSet<Orders> Orders { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Grab the connection string from appsettings.json
            var connectionString = Program.configuration.GetConnectionString("PMDB");

            // Use the SQL Server Entity Framework Core connector
            optionsBuilder.UseSqlServer(connectionString);
        }
    }
}
