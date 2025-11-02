using System;
using System . Collections . Generic;
using System . Data;
using System . Linq;
using System . Text;
using System . Threading . Tasks;

using DocumentFormat . OpenXml . Bibliography;

using Microsoft . AspNetCore . Mvc;
using Microsoft . Data . SqlClient;
using Microsoft . EntityFrameworkCore;

using SalesInventoryV2 . Data;
using SalesInventoryV2 . Models;
using SalesInventoryV2 . Services;

namespace SalesInventoryV2 . Controllers
{
      public class SalesOrdersController : Controller
      {
            private readonly ApplicationDbContext _context;
            private readonly ReportExportService _exportService;


            public SalesOrdersController ( ApplicationDbContext context , ReportExportService exportService )
            {
                  _context = context;
                  _exportService = exportService;
            }

            //   FIXED: Index with pagination and limited data loading
            public async Task<IActionResult> Index ( DateTime? startDate , DateTime? endDate , string status , int page = 1 )
            {
                  const int pageSize = 10000; //   Only load 50 orders at a time!

                  //   Set default date range to last 30 days (not ALL data!)
                  if ( !startDate . HasValue )
                        startDate = DateTime . Now . AddDays ( -30 );
                  if ( !endDate . HasValue )
                        endDate = DateTime . Now;

                  //   Build query with filters
                  var query = _context.SalesOrders
                .Include(so => so.Customer) //   Only load Customer, NOT OrderDetails!
                .Where(o => o.OrderDate >= startDate.Value && o.OrderDate <= endDate.Value);

                  //   Filter by status
                  if ( !string . IsNullOrEmpty ( status ) )
                  {
                        query = query . Where ( o => o . Status == status );
                  }

                  //   Get total count for pagination
                  var totalOrders = await query.CountAsync();

                  //   Get only current page of data
                  var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

                  //   Pass pagination info to view
                  ViewBag . StartDate = startDate . Value . ToString ( "yyyy-MM-dd" );
                  ViewBag . EndDate = endDate . Value . ToString ( "yyyy-MM-dd" );
                  ViewBag . Status = status;
                  ViewBag . CurrentPage = page;
                  ViewBag . TotalPages = ( int ) Math . Ceiling ( totalOrders / ( double ) pageSize );
                  ViewBag . TotalOrders = totalOrders;

                  return View ( orders );
            }

            //   FIXED: Details - still loads full order but only for ONE order
            public async Task<IActionResult> Details ( int id )
            {
                  var order = await _context.SalesOrders
                .Include(so => so.Customer)
                .Include(so => so.OrderDetails)
                    .ThenInclude(od => od.Product)
                        .ThenInclude(p => p.Category)
                .FirstOrDefaultAsync(so => so.OrderId == id);

                  if ( order == null )
                        return NotFound ( );

                  return View ( order );
            }

            //   Async Create GET
            public async Task<IActionResult> Create ( )
            {
                  ViewBag . Customers = await _context . Customers
                      . OrderBy ( c => c . CustomerName )
                      . ToListAsync ( );

                  ViewBag . Products = await _context . Products
                      . Include ( p => p . Category )
                      . Where ( p => p . IsActive )
                      . OrderBy ( p => p . ProductName )
                      . ToListAsync ( );

                  return View ( );
            }

            //   Async Create POST with batched queries
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> Create ( int customerId , List<int> productIds , List<int> quantities )
            {
                  if ( productIds == null || productIds . Count == 0 )
                  {
                        ModelState . AddModelError ( "" , "Please add at least one product" );
                        ViewBag . Customers = await _context . Customers . ToListAsync ( );
                        ViewBag . Products = await _context . Products
                            . Include ( p => p . Category )
                            . Where ( p => p . IsActive )
                            . ToListAsync ( );
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

                  //   Load all products at once (not in loop!)
                  var uniqueProductIds = productIds.Distinct().ToList();
                  var products = await _context.Products
                .Where(p => uniqueProductIds.Contains(p.ProductId))
                .ToDictionaryAsync(p => p.ProductId);

                  for ( int i = 0 ; i < productIds . Count ; i++ )
                  {
                        if ( quantities [ i ] <= 0 ) continue;

                        if ( products . TryGetValue ( productIds [ i ] , out var product ) )
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
                  await _context . SaveChangesAsync ( );

                  TempData [ "SuccessMessage" ] = "Order created successfully!";
                  return RedirectToAction ( nameof ( Details ) , new { id = order . OrderId } );
            }

            //   CRITICAL: AJAX endpoint for Sales Report using Stored Procedure
            [HttpGet]
            public async Task<JsonResult> GetSalesReportData ( DateTime? startDate , DateTime? endDate )
            {
                  try
                  {
                        if ( !startDate . HasValue )
                              startDate = DateTime . Now . AddMonths ( -1 );
                        if ( !endDate . HasValue )
                              endDate = DateTime . Now;

                        var startParam = new SqlParameter("@StartDate", SqlDbType.Date) { Value = startDate.Value };
                        var endParam = new SqlParameter("@EndDate", SqlDbType.Date) { Value = endDate.Value };

                        using ( var command = _context . Database . GetDbConnection ( ) . CreateCommand ( ) )
                        {
                              command . CommandText = "usp_GetSalesReport";
                              command . CommandType = CommandType . StoredProcedure;
                              command . Parameters . Add ( startParam );
                              command . Parameters . Add ( endParam );
                              command . CommandTimeout = 60; // 60 second timeout

                              await _context . Database . OpenConnectionAsync ( );

                              using ( var result = await command . ExecuteReaderAsync ( ) )
                              {
                                    // Initialize with defaults
                                    var summary = new { TotalOrders = 0, TotalRevenue = 0m, CompletedOrders = 0, PendingOrders = 0, CancelledOrders = 0, AverageOrderValue = 0m };
                                    var topProducts = new List<object>();
                                    var topCustomers = new List<object>();
                                    var revenueByCategory = new List<object>();

                                    // Result Set 1: Summary
                                    if ( await result . ReadAsync ( ) )
                                    {
                                          summary = new
                                          {
                                                TotalOrders = result . GetInt32 ( 0 ) ,
                                                TotalRevenue = result . GetDecimal ( 1 ) ,
                                                CompletedOrders = result . GetInt32 ( 2 ) ,
                                                PendingOrders = result . GetInt32 ( 3 ) ,
                                                CancelledOrders = result . GetInt32 ( 4 ) ,
                                                AverageOrderValue = result . GetDecimal ( 5 )
                                          };
                                    }

                                    // Result Set 2: Top Products
                                    await result . NextResultAsync ( );
                                    while ( await result . ReadAsync ( ) )
                                    {
                                          topProducts . Add ( new
                                          {
                                                ProductId = result . GetInt32 ( 0 ) ,
                                                ProductName = result . GetString ( 1 ) ,
                                                TotalQuantitySold = result . GetInt32 ( 2 ) ,
                                                TotalRevenue = result . GetDecimal ( 3 ) ,
                                                OrderCount = result . GetInt32 ( 4 )
                                          } );
                                    }

                                    // Result Set 3: Top Customers
                                    await result . NextResultAsync ( );
                                    while ( await result . ReadAsync ( ) )
                                    {
                                          topCustomers . Add ( new
                                          {
                                                CustomerId = result . GetInt32 ( 0 ) ,
                                                CustomerName = result . GetString ( 1 ) ,
                                                Email = result . IsDBNull ( 2 ) ? "" : result . GetString ( 2 ) ,
                                                City = result . IsDBNull ( 3 ) ? "" : result . GetString ( 3 ) ,
                                                TotalOrders = result . GetInt32 ( 4 ) ,
                                                TotalSpent = result . GetDecimal ( 5 ) ,
                                                AverageOrderValue = result . GetDecimal ( 6 )
                                          } );
                                    }

                                    // Result Set 4: Revenue by Category
                                    await result . NextResultAsync ( );
                                    while ( await result . ReadAsync ( ) )
                                    {
                                          revenueByCategory . Add ( new
                                          {
                                                CategoryId = result . GetInt32 ( 0 ) ,
                                                CategoryName = result . GetString ( 1 ) ,
                                                TotalRevenue = result . GetDecimal ( 2 ) ,
                                                TotalQuantity = result . GetInt32 ( 3 ) ,
                                                OrderCount = result . GetInt32 ( 4 )
                                          } );
                                    }

                                    return Json ( new
                                    {
                                          success = true ,
                                          data = new
                                          {
                                                totalOrders = summary . TotalOrders ,
                                                totalRevenue = summary . TotalRevenue ,
                                                completedOrders = summary . CompletedOrders ,
                                                pendingOrders = summary . PendingOrders ,
                                                cancelledOrders = summary . CancelledOrders ,
                                                averageOrderValue = summary . AverageOrderValue ,
                                                topProducts = topProducts ,
                                                topCustomers = topCustomers ,
                                                revenueByCategory = revenueByCategory
                                          }
                                    } );
                              }
                        }
                  }
                  catch ( Exception ex )
                  {
                        return Json ( new
                        {
                              success = false ,
                              message = "Error: " + ex . Message
                        } );
                  }
            }

            // GET: SalesOrders/SalesReport
            public IActionResult SalesReport ( )
            {
                  return View ( );
            }

            //   Async Delete
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> Delete ( int id )
            {
                  var order = await _context.SalesOrders
                .Include(so => so.OrderDetails)
                .FirstOrDefaultAsync(so => so.OrderId == id);

                  if ( order != null )
                  {
                        _context . SalesOrderDetails . RemoveRange ( order . OrderDetails );
                        _context . SalesOrders . Remove ( order );
                        await _context . SaveChangesAsync ( );

                        TempData [ "SuccessMessage" ] = "Order deleted successfully!";
                  }

                  return RedirectToAction ( nameof ( Index ) );
            }

            //   NEW: Export Sales Report to PDF
            [HttpPost]
            public async Task<IActionResult> ExportSalesReportPdf ( DateTime? startDate , DateTime? endDate )
            {
                  try
                  {
                        // Set defaults
                        if ( !startDate . HasValue )
                              startDate = DateTime . Now . AddMonths ( -1 );
                        if ( !endDate . HasValue )
                              endDate = DateTime . Now;

                        // Get report data from stored procedure
                        var reportData = await GetSalesReportDataForExport(startDate.Value, endDate.Value);

                        // Generate HTML
                        var html = GenerateSalesReportHtml(reportData, startDate.Value, endDate.Value);

                        // Convert to PDF
                        var pdfBytes = _exportService.GeneratePdfFromHtml(html, "Sales Report");

                        // Return PDF file
                        var fileName = $"SalesReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf";
                        return File ( pdfBytes , "application/pdf" , fileName );
                  }
                  catch ( Exception ex )
                  {
                        TempData [ "ErrorMessage" ] = "Error generating PDF: " + ex . Message;
                        return RedirectToAction ( nameof ( SalesReport ) );
                  }
            }

            //   NEW: Export Sales Report to Excel
            [HttpPost]
            public async Task<IActionResult> ExportSalesReportExcel ( DateTime? startDate , DateTime? endDate )
            {
                  try
                  {
                        // Set defaults
                        if ( !startDate . HasValue )
                              startDate = DateTime . Now . AddMonths ( -1 );
                        if ( !endDate . HasValue )
                              endDate = DateTime . Now;

                        // Get orders data
                        var orders = await _context.SalesOrders
                    .Include(so => so.Customer)
                    .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate)
                    .OrderByDescending(o => o.OrderDate)
                    .Select(o => new
                    {
                          OrderNumber = o.OrderNumber,
                          CustomerName = o.Customer.CustomerName,
                          OrderDate = o.OrderDate,
                          TotalAmount = o.TotalAmount,
                          Status = o.Status
                    })
                    .ToListAsync();

                        // Generate Excel
                        var title = $"Sales Report - {startDate:MMM dd, yyyy} to {endDate:MMM dd, yyyy}";
                        var excelBytes = _exportService.GenerateFormattedExcel(
                    orders,
                    "Sales Orders",
                    title,
                    currencyColumns: new List<string> { "TotalAmount" }
                );

                        // Return Excel file
                        var fileName = $"SalesReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx";
                        return File (
                            excelBytes ,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" ,
                            fileName
                        );
                  }
                  catch ( Exception ex )
                  {
                        TempData [ "ErrorMessage" ] = "Error generating Excel: " + ex . Message;
                        return RedirectToAction ( nameof ( SalesReport ) );
                  }
            }

            //   NEW: Export Detailed Sales Report to Excel (Multiple Sheets)
            [HttpPost]
            public async Task<IActionResult> ExportDetailedSalesReportExcel ( DateTime? startDate , DateTime? endDate )
            {
                  try
                  {
                        if ( !startDate . HasValue ) startDate = DateTime . Now . AddMonths ( -1 );
                        if ( !endDate . HasValue ) endDate = DateTime . Now;

                        // Get all report data
                        var orders = await _context.SalesOrders
                    .Include(so => so.Customer)
                    .Include(so => so.OrderDetails)
                        .ThenInclude(od => od.Product)
                            .ThenInclude(p => p.Category)
                    .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate)
                    .ToListAsync();

                        // Prepare sheets data
                        var sheets = new Dictionary<string, object>();

                        // Sheet 1: Summary
                        var summary = new List<dynamic>
                {
                    new {
                        Metric = "Total Orders",
                        Value = orders.Count
                    },
                    new {
                        Metric = "Total Revenue",
                        Value = orders.Sum(o => o.TotalAmount)
                    },
                    new {
                        Metric = "Completed Orders",
                        Value = orders.Count(o => o.Status == "Completed")
                    },
                    new {
                        Metric = "Pending Orders",
                        Value = orders.Count(o => o.Status == "Pending")
                    }
                };
                        sheets . Add ( "Summary" , summary );

                        // Sheet 2: All Orders
                        var ordersData = orders.Select(o => new
                        {
                              OrderNumber = o.OrderNumber,
                              Customer = o.Customer.CustomerName,
                              OrderDate = o.OrderDate,
                              TotalAmount = o.TotalAmount,
                              Status = o.Status,
                              ItemCount = o.OrderDetails.Count
                        }).ToList();
                        sheets . Add ( "All Orders" , ordersData );

                        // Sheet 3: Top Products
                        var topProducts = orders
                    .SelectMany(o => o.OrderDetails)
                    .GroupBy(od => new { od.Product.ProductName, od.Product.Category.CategoryName })
                    .Select(g => new
                    {
                          Product = g.Key.ProductName,
                          Category = g.Key.CategoryName,
                          TotalQuantity = g.Sum(od => od.Quantity),
                          TotalRevenue = g.Sum(od => od.SubTotal)
                    })
                    .OrderByDescending(p => p.TotalRevenue)
                    .Take(20)
                    .ToList();
                        sheets . Add ( "Top Products" , topProducts );

                        // Sheet 4: Top Customers
                        var topCustomers = orders
                    .GroupBy(o => o.Customer.CustomerName)
                    .Select(g => new
                    {
                          Customer = g.Key,
                          TotalOrders = g.Count(),
                          TotalSpent = g.Sum(o => o.TotalAmount)
                    })
                    .OrderByDescending(c => c.TotalSpent)
                    .Take(20)
                    .ToList();
                        sheets . Add ( "Top Customers" , topCustomers );

                        // Generate Excel
                        var excelBytes = _exportService.GenerateExcelMultiSheet(
                    sheets,
                    $"Detailed Sales Report - {startDate:MMM dd, yyyy} to {endDate:MMM dd, yyyy}"
                );

                        var fileName = $"DetailedSalesReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx";
                        return File (
                            excelBytes ,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" ,
                            fileName
                        );
                  }
                  catch ( Exception ex )
                  {
                        TempData [ "ErrorMessage" ] = "Error generating detailed Excel: " + ex . Message;
                        return RedirectToAction ( nameof ( SalesReport ) );
                  }
            }

            //   Helper: Get report data for export
            private async Task<dynamic> GetSalesReportDataForExport ( DateTime startDate , DateTime endDate )
            {
                  var orders = await _context.SalesOrders
                .Include(so => so.Customer)
                .Include(so => so.OrderDetails)
                    .ThenInclude(od => od.Product)
                        .ThenInclude(p => p.Category)
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate)
                .ToListAsync();

                  return new
                  {
                        TotalOrders = orders . Count ,
                        TotalRevenue = orders . Sum ( o => o . TotalAmount ) ,
                        CompletedOrders = orders . Count ( o => o . Status == "Completed" ) ,
                        PendingOrders = orders . Count ( o => o . Status == "Pending" ) ,
                        CancelledOrders = orders . Count ( o => o . Status == "Cancelled" ) ,

                        TopProducts = orders
                          . SelectMany ( o => o . OrderDetails )
                          . GroupBy ( od => new { od . Product . ProductName } )
                          . Select ( g => new
                          {
                                ProductName = g . Key . ProductName ,
                                TotalQuantity = g . Sum ( od => od . Quantity ) ,
                                TotalRevenue = g . Sum ( od => od . SubTotal )
                          } )
                          . OrderByDescending ( p => p . TotalRevenue )
                          . Take ( 10 )
                          . ToList ( ) ,

                        TopCustomers = orders
                          . GroupBy ( o => new { o . Customer . CustomerName , o . Customer . City } )
                          . Select ( g => new
                          {
                                CustomerName = g . Key . CustomerName ,
                                City = g . Key . City ,
                                TotalOrders = g . Count ( ) ,
                                TotalSpent = g . Sum ( o => o . TotalAmount )
                          } )
                          . OrderByDescending ( c => c . TotalSpent )
                          . Take ( 10 )
                          . ToList ( ) ,

                        RevenueByCategory = orders
                          . SelectMany ( o => o . OrderDetails )
                          . GroupBy ( od => od . Product . Category . CategoryName )
                          . Select ( g => new
                          {
                                CategoryName = g . Key ,
                                TotalRevenue = g . Sum ( od => od . SubTotal )
                          } )
                          . OrderByDescending ( c => c . TotalRevenue )
                          . ToList ( )
                  };
            }


            private string GenerateSalesReportHtml ( dynamic reportData , DateTime startDate , DateTime endDate )
                  {
                        var html = new StringBuilder();
                        html . Append ( $@"
      <!DOCTYPE html>
      <html>
      <head>
          <meta charset='utf-8'>
          <style>
              body {{ 
                  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; 
                  margin: 20px;
                  color: #333;
              }}
              h1 {{ 
                  color: #2c3e50; 
                  text-align: center; 
                  border-bottom: 3px solid #3498db;
                  padding-bottom: 10px;
              }}
              .report-header {{
                  text-align: center;
                  margin-bottom: 30px;
                  padding: 15px;
                  background-color: #ecf0f1;
                  border-radius: 5px;
              }}
              .summary {{ 
                  background-color: #e8f8f5; 
                  padding: 20px; 
                  margin: 20px 0; 
                  border-left: 4px solid #1abc9c;
                  border-radius: 5px;
              }}
              .summary-grid {{
                  display: grid;
                  grid-template-columns: repeat(2, 1fr);
                  gap: 15px;
                  margin-top: 15px;
              }}
              .summary-item {{
                  background: white;
                  padding: 10px;
                  border-radius: 5px;
                  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
              }}
              .summary-label {{
                  font-size: 12px;
                  color: #7f8c8d;
                  text-transform: uppercase;
              }}
              .summary-value {{
                  font-size: 24px;
                  font-weight: bold;
                  color: #2c3e50;
                  margin-top: 5px;
              }}
              table {{ 
                  width: 100%; 
                  border-collapse: collapse; 
                  margin: 20px 0; 
                  background: white;
                  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
              }}
              th {{ 
                  background-color: #3498db; 
                  color: white; 
                  padding: 12px; 
                  text-align: left;
                  font-weight: 600;
              }}
              td {{ 
                  border-bottom: 1px solid #ecf0f1; 
                  padding: 10px; 
              }}
              tr:nth-child(even) {{ 
                  background-color: #f8f9fa; 
              }}
              tr:hover {{
                  background-color: #e8f6f9;
              }}
              .section-title {{
                  font-size: 18px;
                  font-weight: bold;
                  color: #2c3e50;
                  margin-top: 30px;
                  margin-bottom: 15px;
                  padding-bottom: 5px;
                  border-bottom: 2px solid #3498db;
              }}
              .footer {{
                  margin-top: 40px;
                  text-align: center;
                  color: #95a5a6;
                  font-size: 12px;
                  padding-top: 20px;
                  border-top: 1px solid #ecf0f1;
              }}
          </style>
      </head>
      <body>
          <div class='report-header'>
              <h1>📊 Sales Report</h1>
              <p style='font-size: 14px; color: #7f8c8d;'>
                  Period: <strong>{startDate:MMMM dd, yyyy}</strong> to <strong>{endDate:MMMM dd, yyyy}</strong>
              </p>
          </div>
    
          <div class='summary'>
              <h3 style='margin-top: 0;'>📈 Summary Statistics</h3>
              <div class='summary-grid'>
                  <div class='summary-item'>
                      <div class='summary-label'>Total Orders</div>
                      <div class='summary-value'>{reportData . TotalOrders}</div>
                  </div>
                  <div class='summary-item'>
                      <div class='summary-label'>Total Revenue</div>
                      <div class='summary-value' style='color: #27ae60;'>${reportData . TotalRevenue:N2}</div>
                  </div>
                  <div class='summary-item'>
                      <div class='summary-label'>Completed</div>
                      <div class='summary-value' style='color: #3498db;'>{reportData . CompletedOrders}</div>
                  </div>
                  <div class='summary-item'>
                      <div class='summary-label'>Pending</div>
                      <div class='summary-value' style='color: #f39c12;'>{reportData . PendingOrders}</div>
                  </div>
              </div>
          </div>
    
          <div class='section-title'>🏆 Top 10 Products by Revenue</div>
          <table>
              <thead>
                  <tr>
                      <th style='width: 10%;'>#</th>
                      <th style='width: 50%;'>Product Name</th>
                      <th style='width: 20%;'>Quantity Sold</th>
                      <th style='width: 20%;'>Total Revenue</th>
                  </tr>
              </thead>
              <tbody>" );

                        int productRank = 1;
                        foreach ( var product in reportData . TopProducts )
                        {
                              html . Append ( $@"
                  <tr>
                      <td style='text-align: center; font-weight: bold;'>{productRank}</td>
                      <td>{product . ProductName}</td>
                      <td style='text-align: center;'>{product . TotalQuantity}</td>
                      <td style='text-align: right; color: #27ae60; font-weight: bold;'>${product . TotalRevenue:N2}</td>
                  </tr>" );
                              productRank++;
                        }

                        html . Append ( $@"
              </tbody>
          </table>
    
          <div class='section-title'>👥 Top 10 Customers</div>
          <table>
              <thead>
                  <tr>
                      <th style='width: 10%;'>#</th>
                      <th style='width: 40%;'>Customer Name</th>
                      <th style='width: 20%;'>City</th>
                      <th style='width: 15%;'>Orders</th>
                      <th style='width: 15%;'>Total Spent</th>
                  </tr>
              </thead>
              <tbody>" );

                        int customerRank = 1;
                        foreach ( var customer in reportData . TopCustomers )
                        {
                              html . Append ( $@"
                  <tr>
                      <td style='text-align: center; font-weight: bold;'>{customerRank}</td>
                      <td>{customer . CustomerName}</td>
                      <td>{customer . City}</td>
                      <td style='text-align: center;'>{customer . TotalOrders}</td>
                      <td style='text-align: right; color: #27ae60; font-weight: bold;'>${customer . TotalSpent:N2}</td>
                  </tr>" );
                              customerRank++;
                        }

                        html . Append ( $@"
              </tbody>
          </table>
    
          <div class='section-title'>📊 Revenue by Category</div>
          <table>
              <thead>
                  <tr>
                      <th style='width: 60%;'>Category</th>
                      <th style='width: 40%;'>Total Revenue</th>
                  </tr>
              </thead>
              <tbody>" );

                        foreach ( var category in reportData . RevenueByCategory )
                        {
                              html . Append ( $@"
                  <tr>
                      <td>{category . CategoryName}</td>
                      <td style='text-align: right; color: #27ae60; font-weight: bold;'>${category . TotalRevenue:N2}</td>
                  </tr>" );
                        }

                        html . Append ( $@"
              </tbody>
          </table>
    
          <div class='footer'>
              <p>Generated on {DateTime . Now:MMMM dd, yyyy hh:mm tt}</p>
              <p>Sales & Inventory Management System</p>
          </div>
      </body>
      </html>" );

                        return html . ToString ( );
                  }
            
      }

}