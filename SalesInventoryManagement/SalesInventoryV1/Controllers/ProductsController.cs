using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesInventoryV1.Data;
using SalesInventoryV1.Models;
using System.Linq;

namespace SalesInventoryV1.Controllers
{
    public class ProductsController(ApplicationDbContext context) : Controller
    {

        public IActionResult Index(string searchTerm, int? categoryId)
        {
            // Load ALL products with related Category (no filtering at DB level)
            var products = context.Products
                .Include(p => p.Category)  //   Eager loading without filtering
                .ToList();  //   Brings ALL data to memory

            // Client-side filtering (SLOW!)
            if (!string.IsNullOrEmpty(searchTerm))
            {
                products = products
                    .Where(p => p.ProductName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (categoryId.HasValue)
            {
                products = products.Where(p => p.CategoryId == categoryId.Value).ToList();
            }

            // Load categories for dropdown (synchronously)
            ViewBag.Categories = context.Categories.ToList();

            return View(products);
        }

        //   Synchronous Details page
        public IActionResult Details(int id)
        {
            var product = context.Products
                .Include(p => p.Category)
                .FirstOrDefault(p => p.ProductId == id);

            if (product == null)
                return NotFound();

            return View(product);
        }

        // CREATE - GET
        public IActionResult Create()
        {
            ViewBag.Categories = context.Categories.ToList();
            return View();
        }

        //   Synchronous POST with no validation optimizations
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Product product)
        {
            if (ModelState.IsValid)
            {
                product.CreatedDate = DateTime.Now;
                product.IsActive = true;

                context.Products.Add(product);
                context.SaveChanges();  //   Synchronous save

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Categories = context.Categories.ToList();
            return View(product);
        }

        // EDIT - GET
        public IActionResult Edit(int id)
        {
            var product = context.Products.Find(id);  //   Synchronous Find

            if (product == null)
                return NotFound();

            ViewBag.Categories = context.Categories.ToList();
            return View(product);
        }

        //   Synchronous POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Product product)
        {
            if (id != product.ProductId)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    context.Update(product);
                    context.SaveChanges();  //   Synchronous save
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.ProductId))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Categories = context.Categories.ToList();
            return View(product);
        }

        // DELETE - GET
        public IActionResult Delete(int id)
        {
            var product = context.Products
                .Include(p => p.Category)
                .FirstOrDefault(p => p.ProductId == id);

            if (product == null)
                return NotFound();

            return View(product);
        }

        //   Synchronous DELETE
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var product = context.Products.Find(id);
            if (product != null)
            {
                context.Products.Remove(product);
                context.SaveChanges();  //   Synchronous save
            }

            return RedirectToAction(nameof(Index));
        }

        //   Synchronous helper method
        private bool ProductExists(int id)
        {
            return context.Products.Any(e => e.ProductId == id);
        }

        // BOTTLENECK #4: Low Stock Report - NO stored procedure, client-side filtering
        public IActionResult LowStockReport()
        {
            // Load ALL products into memory
            var products = context.Products
                .Include(p => p.Category)
                .ToList();  //   Load everything

            // Client-side filtering
            var lowStockProducts = products
                .Where(p => p.StockQuantity <= p.ReorderLevel && p.IsActive)
                .OrderBy(p => p.StockQuantity)
                .ToList();

            return View(lowStockProducts);
        }
    }
}