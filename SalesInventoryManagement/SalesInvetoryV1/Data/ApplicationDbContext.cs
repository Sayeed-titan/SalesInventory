using Microsoft . EntityFrameworkCore;

using SalesInventoryV1 . Models;

namespace SalesInventoryV1 . Data
{
      public class ApplicationDbContext : DbContext
      {
            public ApplicationDbContext ( DbContextOptions<ApplicationDbContext> options )
                : base ( options )
            {
            }

            public DbSet<Category> Categories { get; set; }
            public DbSet<Product> Products { get; set; }
            public DbSet<Customer> Customers { get; set; }
            public DbSet<SalesOrder> SalesOrders { get; set; }
            public DbSet<SalesOrderDetail> SalesOrderDetails { get; set; }

            protected override void OnModelCreating ( ModelBuilder modelBuilder )
            {
                  base . OnModelCreating ( modelBuilder );

                  // Configure relationships
                  modelBuilder . Entity<Product> ( )
                      . HasOne ( p => p . Category )
                      . WithMany ( c => c . Products )
                      . HasForeignKey ( p => p . CategoryId );

                  modelBuilder . Entity<SalesOrder> ( )
                      . HasOne ( so => so . Customer )
                      . WithMany ( c => c . SalesOrders )
                      . HasForeignKey ( so => so . CustomerId );

                  modelBuilder . Entity<SalesOrderDetail> ( )
                      . HasOne ( sod => sod . SalesOrder )
                      . WithMany ( so => so . OrderDetails )
                      . HasForeignKey ( sod => sod . OrderId );

                  modelBuilder.Entity<SalesOrderDetail>()
                        .HasOne(sod => sod.Product)
                        .WithMany()
                        .HasForeignKey ( sod => sod . ProductId );
            }
      }
}