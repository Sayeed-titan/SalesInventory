using Microsoft . AspNetCore . Mvc;
using Microsoft . EntityFrameworkCore;

using System;
using System . Linq;
using System . Collections . Generic;
using SalesInventoryV2 . Data;
using SalesInventoryV2 . Models;

namespace SalesInventoryV1 . Controllers
{
      public class SalesOrdersController : Controller
      {
            private readonly ApplicationDbContext _context;

            public SalesOrdersController ( ApplicationDbContext context )
            {
                  _context = context;
            }

            // GET: SalesOrders/Index
            public IActionResult Index ( DateTime? startDate , DateTime? endDate , string status )
            {
                  // Load orders with Customer - NO stored procedure
                  var orders = _context.SalesOrders
                .Include(so => so.Customer)
                .ToList();

                  // Client-side filtering
                  if ( startDate . HasValue )
                  {
                        orders = orders . Where ( o => o . OrderDate >= startDate . Value ) . ToList ( );
                  }

                  if ( endDate . HasValue )
                  {
                        orders = orders . Where ( o => o . OrderDate <= endDate . Value ) . ToList ( );
                  }

                  if ( !string . IsNullOrEmpty ( status ) )
                  {
                        orders = orders . Where ( o => o . Status == status ) . ToList ( );
                  }

                  orders = orders . OrderByDescending ( o => o . OrderDate ) . ToList ( );

                  ViewBag . StartDate = startDate?.ToString ( "yyyy-MM-dd" );
                  ViewBag . EndDate = endDate?.ToString ( "yyyy-MM-dd" );
                  ViewBag . Status = status;

                  return View ( orders );
            }

            // GET: SalesOrders/Details/5
            public IActionResult Details ( int id )
            {
                  var order = _context.SalesOrders
                .Include(so => so.Customer)
                .Include(so => so.OrderDetails)
                    .ThenInclude(od => od.Product)
                        .ThenInclude(p => p.Category)
                .FirstOrDefault(so => so.OrderId == id);

                  if ( order == null )
                        return NotFound ( );

                  return View ( order );
            }

            // GET: SalesOrders/Create
            public IActionResult Create ( )
            {
                  ViewBag . Customers = _context . Customers . OrderBy ( c => c . CustomerName ) . ToList ( );
                  ViewBag . Products = _context . Products
                      . Include ( p => p . Category )
                      . Where ( p => p . IsActive )
                      . OrderBy ( p => p . ProductName )
                      . ToList ( );
                  return View ( );
            }

            // POST: SalesOrders/Create
            [HttpPost]
            [ValidateAntiForgeryToken]
            public IActionResult Create ( int customerId , List<int> productIds , List<int> quantities )
            {
                  if ( productIds == null || productIds . Count == 0 )
                  {
                        ModelState . AddModelError ( "" , "Please add at least one product" );
                        ViewBag . Customers = _context . Customers . ToList ( );
                        ViewBag . Products = _context . Products
                            . Include ( p => p . Category )
                            . Where ( p => p . IsActive )
                            . ToList ( );
                        return View ( );
                  }

                  var orderNumber = "ORD-" + DateTime.Now.ToString("yyyyMMddHHmmss");

                  var order = new SalesOrder
                  {
                        OrderNumber = orderNumber,
                        CustomerId = customerId,
                        OrderDate = DateTime.Now,
                        Status = "Pending",
                        CreatedDate = DateTime.Now,
                        OrderDetails = new List<SalesOrderDetail>()
                  };

                  decimal totalAmount = 0;

                  for ( int i = 0 ; i < productIds . Count ; i++ )
                  {
                        if ( quantities [ i ] <= 0 ) continue;

                        var product = _context.Products.Find(productIds[i]);
                        if ( product != null )
                        {
                              var subTotal = product.UnitPrice * quantities[i];
                              totalAmount += subTotal;

                              order . OrderDetails . Add ( new SalesOrderDetail
                              {
                                    ProductId = productIds [ i ] ,
                                    Quantity = quantities [ i ] ,
                                    UnitPrice = product . UnitPrice ,
                                    SubTotal = subTotal
                              } );
                        }
                  }

                  order . TotalAmount = totalAmount;

                  _context . SalesOrders . Add ( order );
                  _context . SaveChanges ( );

                  TempData [ "SuccessMessage" ] = "Order created successfully!";
                  return RedirectToAction ( nameof ( Details ) , new { id = order . OrderId } );
            }

            // GET: SalesOrders/SalesReport
            // THIS IS THE SLOW VERSION - Will be optimized in V2
            public IActionResult SalesReport ( DateTime? startDate , DateTime? endDate )
            {
                  if ( !startDate . HasValue )
                        startDate = DateTime . Now . AddMonths ( -1 );
                  if ( !endDate . HasValue )
                        endDate = DateTime . Now;

                  // Load ALL orders - SLOW with 10,000 records!
                  var orders = _context.SalesOrders
                .Include(so => so.Customer)
                .Include(so => so.OrderDetails)
                    .ThenInclude(od => od.Product)
                        .ThenInclude(p => p.Category)
                .ToList();

                  // Client-side filtering
                  var filteredOrders = orders
                .Where(o => o.OrderDate >= startDate.Value && o.OrderDate <= endDate.Value)
                .ToList();

                  // Complex aggregations in C#
                  var reportData = new
                  {
                        TotalOrders = filteredOrders.Count,
                        TotalRevenue = filteredOrders.Sum(o => o.TotalAmount),
                        CompletedOrders = filteredOrders.Count(o => o.Status == "Completed"),
                        PendingOrders = filteredOrders.Count(o => o.Status == "Pending"),
                        CancelledOrders = filteredOrders.Count(o => o.Status == "Cancelled"),

                        TopProducts = filteredOrders
                    .SelectMany(o => o.OrderDetails)
                    .GroupBy(od => new { od.ProductId, od.Product.ProductName })
                    .Select(g => new
                    {
                          ProductName = g.Key.ProductName,
                          TotalQuantity = g.Sum(od => od.Quantity),
                          TotalRevenue = g.Sum(od => od.SubTotal)
                    })
                    .OrderByDescending(p => p.TotalRevenue)
                    .Take(10)
                    .ToList(),

                        TopCustomers = filteredOrders
                    .GroupBy(o => new { o.CustomerId, o.Customer.CustomerName, o.Customer.City })
                    .Select(g => new
                    {
                          CustomerName = g.Key.CustomerName,
                          City = g.Key.City,
                          TotalOrders = g.Count(),
                          TotalSpent = g.Sum(o => o.TotalAmount)
                    })
                    .OrderByDescending(c => c.TotalSpent)
                    .Take(10)
                    .ToList(),

                        RevenueByCategory = filteredOrders
                    .SelectMany(o => o.OrderDetails)
                    .GroupBy(od => od.Product.Category.CategoryName)
                    .Select(g => new
                    {
                          CategoryName = g.Key,
                          TotalRevenue = g.Sum(od => od.SubTotal)
                    })
                    .OrderByDescending(c => c.TotalRevenue)
                    .ToList()
                  };

                  ViewBag . StartDate = startDate . Value . ToString ( "yyyy-MM-dd" );
                  ViewBag . EndDate = endDate . Value . ToString ( "yyyy-MM-dd" );

                  return View ( reportData );
            }

            // POST: SalesOrders/Delete
            [HttpPost]
            [ValidateAntiForgeryToken]
            public IActionResult Delete ( int id )
            {
                  var order = _context.SalesOrders
                .Include(so => so.OrderDetails)
                .FirstOrDefault(so => so.OrderId == id);

                  if ( order != null )
                  {
                        _context . SalesOrderDetails . RemoveRange ( order . OrderDetails );
                        _context . SalesOrders . Remove ( order );
                        _context . SaveChanges ( );

                        TempData [ "SuccessMessage" ] = "Order deleted successfully!";
                  }

                  return RedirectToAction ( nameof ( Index ) );
            }
      }
}