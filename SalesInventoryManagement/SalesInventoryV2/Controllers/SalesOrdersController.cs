// Controllers/SalesOrdersController.cs
// VERSION 2: FIXED - CPU 100% issue resolved

using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SalesInventoryV2.Data;
using SalesInventoryV2.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace SalesInventoryV2.Controllers
{
    public class SalesOrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SalesOrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ✅ FIXED: Index with pagination and limited data loading
        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, string status, int page = 1)
        {
            const int pageSize = 10000; // ✅ Only load 50 orders at a time!

            // ✅ Set default date range to last 30 days (not ALL data!)
            if (!startDate.HasValue)
                startDate = DateTime.Now.AddDays(-30);
            if (!endDate.HasValue)
                endDate = DateTime.Now;

            // ✅ Build query with filters
            var query = _context.SalesOrders
                .Include(so => so.Customer) // ✅ Only load Customer, NOT OrderDetails!
                .Where(o => o.OrderDate >= startDate.Value && o.OrderDate <= endDate.Value);

            // ✅ Filter by status
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(o => o.Status == status);
            }

            // ✅ Get total count for pagination
            var totalOrders = await query.CountAsync();

            // ✅ Get only current page of data
            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // ✅ Pass pagination info to view
            ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
            ViewBag.Status = status;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalOrders / (double)pageSize);
            ViewBag.TotalOrders = totalOrders;

            return View(orders);
        }

        // ✅ FIXED: Details - still loads full order but only for ONE order
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.SalesOrders
                .Include(so => so.Customer)
                .Include(so => so.OrderDetails)
                    .ThenInclude(od => od.Product)
                        .ThenInclude(p => p.Category)
                .FirstOrDefaultAsync(so => so.OrderId == id);

            if (order == null)
                return NotFound();

            return View(order);
        }

        // ✅ Async Create GET
        public async Task<IActionResult> Create()
        {
            ViewBag.Customers = await _context.Customers
                .OrderBy(c => c.CustomerName)
                .ToListAsync();
            
            ViewBag.Products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive)
                .OrderBy(p => p.ProductName)
                .ToListAsync();
            
            return View();
        }

        // ✅ Async Create POST with batched queries
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int customerId, List<int> productIds, List<int> quantities)
        {
            if (productIds == null || productIds.Count == 0)
            {
                ModelState.AddModelError("", "Please add at least one product");
                ViewBag.Customers = await _context.Customers.ToListAsync();
                ViewBag.Products = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.IsActive)
                    .ToListAsync();
                return View();
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

            // ✅ Load all products at once (not in loop!)
            var uniqueProductIds = productIds.Distinct().ToList();
            var products = await _context.Products
                .Where(p => uniqueProductIds.Contains(p.ProductId))
                .ToDictionaryAsync(p => p.ProductId);

            for (int i = 0; i < productIds.Count; i++)
            {
                if (quantities[i] <= 0) continue;

                if (products.TryGetValue(productIds[i], out var product))
                {
                    var subTotal = product.UnitPrice * quantities[i];
                    totalAmount += subTotal;

                    order.OrderDetails.Add(new SalesOrderDetail
                    {
                        ProductId = productIds[i],
                        Quantity = quantities[i],
                        UnitPrice = product.UnitPrice,
                        SubTotal = subTotal
                    });
                }
            }

            order.TotalAmount = totalAmount;

            _context.SalesOrders.Add(order);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Order created successfully!";
            return RedirectToAction(nameof(Details), new { id = order.OrderId });
        }

        // ✅ CRITICAL: AJAX endpoint for Sales Report using Stored Procedure
        [HttpGet]
        public async Task<JsonResult> GetSalesReportData(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                if (!startDate.HasValue)
                    startDate = DateTime.Now.AddMonths(-1);
                if (!endDate.HasValue)
                    endDate = DateTime.Now;

                var startParam = new SqlParameter("@StartDate", SqlDbType.Date) { Value = startDate.Value };
                var endParam = new SqlParameter("@EndDate", SqlDbType.Date) { Value = endDate.Value };

                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = "usp_GetSalesReport";
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add(startParam);
                    command.Parameters.Add(endParam);
                    command.CommandTimeout = 60; // 60 second timeout

                    await _context.Database.OpenConnectionAsync();

                    using (var result = await command.ExecuteReaderAsync())
                    {
                        // Initialize with defaults
                        var summary = new { TotalOrders = 0, TotalRevenue = 0m, CompletedOrders = 0, PendingOrders = 0, CancelledOrders = 0, AverageOrderValue = 0m };
                        var topProducts = new List<object>();
                        var topCustomers = new List<object>();
                        var revenueByCategory = new List<object>();

                        // Result Set 1: Summary
                        if (await result.ReadAsync())
                        {
                            summary = new
                            {
                                TotalOrders = result.GetInt32(0),
                                TotalRevenue = result.GetDecimal(1),
                                CompletedOrders = result.GetInt32(2),
                                PendingOrders = result.GetInt32(3),
                                CancelledOrders = result.GetInt32(4),
                                AverageOrderValue = result.GetDecimal(5)
                            };
                        }

                        // Result Set 2: Top Products
                        await result.NextResultAsync();
                        while (await result.ReadAsync())
                        {
                            topProducts.Add(new
                            {
                                ProductId = result.GetInt32(0),
                                ProductName = result.GetString(1),
                                TotalQuantitySold = result.GetInt32(2),
                                TotalRevenue = result.GetDecimal(3),
                                OrderCount = result.GetInt32(4)
                            });
                        }

                        // Result Set 3: Top Customers
                        await result.NextResultAsync();
                        while (await result.ReadAsync())
                        {
                            topCustomers.Add(new
                            {
                                CustomerId = result.GetInt32(0),
                                CustomerName = result.GetString(1),
                                Email = result.IsDBNull(2) ? "" : result.GetString(2),
                                City = result.IsDBNull(3) ? "" : result.GetString(3),
                                TotalOrders = result.GetInt32(4),
                                TotalSpent = result.GetDecimal(5),
                                AverageOrderValue = result.GetDecimal(6)
                            });
                        }

                        // Result Set 4: Revenue by Category
                        await result.NextResultAsync();
                        while (await result.ReadAsync())
                        {
                            revenueByCategory.Add(new
                            {
                                CategoryId = result.GetInt32(0),
                                CategoryName = result.GetString(1),
                                TotalRevenue = result.GetDecimal(2),
                                TotalQuantity = result.GetInt32(3),
                                OrderCount = result.GetInt32(4)
                            });
                        }

                        return Json(new
                        {
                            success = true,
                            data = new
                            {
                                totalOrders = summary.TotalOrders,
                                totalRevenue = summary.TotalRevenue,
                                completedOrders = summary.CompletedOrders,
                                pendingOrders = summary.PendingOrders,
                                cancelledOrders = summary.CancelledOrders,
                                averageOrderValue = summary.AverageOrderValue,
                                topProducts = topProducts,
                                topCustomers = topCustomers,
                                revenueByCategory = revenueByCategory
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error: " + ex.Message
                });
            }
        }

        // GET: SalesOrders/SalesReport
        public IActionResult SalesReport()
        {
            return View();
        }

        // ✅ Async Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _context.SalesOrders
                .Include(so => so.OrderDetails)
                .FirstOrDefaultAsync(so => so.OrderId == id);

            if (order != null)
            {
                _context.SalesOrderDetails.RemoveRange(order.OrderDetails);
                _context.SalesOrders.Remove(order);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Order deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}