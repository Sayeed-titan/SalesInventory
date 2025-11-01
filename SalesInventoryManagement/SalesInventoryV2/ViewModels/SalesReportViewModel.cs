using System;
using System . Collections . Generic;
using System . ComponentModel . DataAnnotations . Schema;

namespace SalesInventoryV2 . ViewModels
{
      // Main report container
      public class SalesReportViewModel
      {
            public int TotalOrders { get; set; }
            public decimal TotalRevenue { get; set; }
            public int CompletedOrders { get; set; }
            public int PendingOrders { get; set; }
            public int CancelledOrders { get; set; }
            public decimal AverageOrderValue { get; set; }

            public List<TopProductViewModel> TopProducts { get; set; }
            public List<TopCustomerViewModel> TopCustomers { get; set; }
            public List<CategoryRevenueViewModel> RevenueByCategory { get; set; }
            public List<DailyRevenueViewModel> DailyRevenueTrend { get; set; }
      }

      // Top Products
      public class TopProductViewModel
      {
            public int ProductId { get; set; }
            public string ProductName { get; set; }
            public int TotalQuantitySold { get; set; }
            public decimal TotalRevenue { get; set; }
            public int OrderCount { get; set; }
      }

      // Top Customers
      public class TopCustomerViewModel
      {
            public int CustomerId { get; set; }
            public string CustomerName { get; set; }
            public string Email { get; set; }
            public string City { get; set; }
            public int TotalOrders { get; set; }
            public decimal TotalSpent { get; set; }
            public decimal AverageOrderValue { get; set; }
      }

      // Category Revenue
      public class CategoryRevenueViewModel
      {
            public int CategoryId { get; set; }
            public string CategoryName { get; set; }
            public decimal TotalRevenue { get; set; }
            public int TotalQuantity { get; set; }
            public int OrderCount { get; set; }
      }

      // Daily Revenue Trend
      public class DailyRevenueViewModel
      {
            public DateTime OrderDate { get; set; }
            public int OrderCount { get; set; }
            public decimal DailyRevenue { get; set; }
      }

      // For Order Details page
      public class OrderDetailsViewModel
      {
            public int OrderId { get; set; }
            public string OrderNumber { get; set; }
            public DateTime OrderDate { get; set; }
            public string Status { get; set; }
            public decimal TotalAmount { get; set; }

            public int CustomerId { get; set; }
            public string CustomerName { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string Address { get; set; }
            public string City { get; set; }
            public string Country { get; set; }

            public List<OrderItemViewModel> OrderItems { get; set; }
      }

      public class OrderItemViewModel
      {
            public int OrderDetailId { get; set; }
            public int ProductId { get; set; }
            public string ProductName { get; set; }
            public string CategoryName { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal SubTotal { get; set; }
      }

      // For Order List (from usp_GetSalesOrders)
      [NotMapped]
      public class SalesOrderListItem
      {
            public int OrderId { get; set; }
            public string OrderNumber { get; set; }
            public int CustomerId { get; set; }
            public string CustomerName { get; set; }
            public string Email { get; set; }
            public DateTime OrderDate { get; set; }
            public decimal TotalAmount { get; set; }
            public string Status { get; set; }
            public int ItemCount { get; set; }
      }

      // For Low Stock Report (from usp_GetLowStockProducts)
      [NotMapped]
      public class LowStockProduct
      {
            public int ProductId { get; set; }
            public string ProductName { get; set; }
            public string CategoryName { get; set; }
            public decimal UnitPrice { get; set; }
            public int StockQuantity { get; set; }
            public int ReorderLevel { get; set; }
            public int QuantityToOrder { get; set; }
      }
}

