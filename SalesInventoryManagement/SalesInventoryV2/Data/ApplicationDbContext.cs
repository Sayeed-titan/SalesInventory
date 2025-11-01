using Microsoft . EntityFrameworkCore;

using SalesInventoryV2 . Models;
using SalesInventoryV2 . ViewModels;

namespace SalesInventoryV2 . Data
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

            //For stored procedure results
            public DbSet<SalesOrderListItem> SalesOrderListItems { get; set; }
            public DbSet<LowStockProduct> LowStockProducts { get; set; }

            protected override void OnModelCreating ( ModelBuilder modelBuilder )
            {
                  base . OnModelCreating ( modelBuilder );

                 // Configure keyless entities for SP results
                  modelBuilder . Entity<SalesOrderListItem> ( ) . HasNoKey ( ) ;
                  modelBuilder . Entity<LowStockProduct> ( ) . HasNoKey ( );

                  // ===== Configure Entity Relationships =====
                  modelBuilder . Entity<Product> ( )
                      . HasOne ( p => p . Category )
                      . WithMany ( c => c . Products )
                      . HasForeignKey ( p => p . CategoryId )
                      . OnDelete ( DeleteBehavior . Restrict );

                  modelBuilder . Entity<SalesOrder> ( )
                      . HasOne ( so => so . Customer )
                      . WithMany ( c => c . SalesOrders )
                      . HasForeignKey ( so => so . CustomerId )
                      . OnDelete ( DeleteBehavior . Restrict );

                  modelBuilder . Entity<SalesOrderDetail> ( )
                      . HasOne ( sod => sod . SalesOrder )
                      . WithMany ( so => so . OrderDetails )
                      . HasForeignKey ( sod => sod . OrderId )
                      . OnDelete ( DeleteBehavior . Cascade );

                  modelBuilder . Entity<SalesOrderDetail> ( )
                      . HasOne ( sod => sod . Product )
                      . WithMany ( )
                      . HasForeignKey ( sod => sod . ProductId )
                      . OnDelete ( DeleteBehavior . Restrict );
            }
      }
}