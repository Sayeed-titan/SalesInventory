using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesInventoryV2.Data;
using SalesInventoryV2.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SalesInventoryV2.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;
        }

        //   OPTIMIZED: Async Index with pagination and server-side filtering
        public async Task<IActionResult> Index(string searchTerm, int? categoryId, int page = 1)
        {
            const int pageSize = 50; //   Load 50 products per page

            //   Build query with server-side filters
            var query = _context.Products
                .Include(p => p.Category)
                .AsQueryable();

            //   Server-side search filtering
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(p => p.ProductName.Contains(searchTerm));
            }

            //   Server-side category filtering
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            //   Get total count for pagination
            var totalProducts = await query.CountAsync();

            //   Get only current page of data
            var products = await query
                .OrderBy(p => p.ProductName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            //   Load categories for dropdown (cached query)
            ViewBag.Categories = await _context.Categories
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            //   Pass filter and pagination data to view
            ViewBag.SearchTerm = searchTerm;
            ViewBag.CategoryId = categoryId;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalProducts / (double)pageSize);
            ViewBag.TotalProducts = totalProducts;

            return View(products);
        }

        //   OPTIMIZED: Async Details
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
                return NotFound();

            return View(product);
        }

        //   OPTIMIZED: Async Create GET
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = await _context.Categories
                .OrderBy(c => c.CategoryName)
                .ToListAsync();
            return View();
        }

        //   OPTIMIZED: Async Create POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product)
        {
            if (ModelState.IsValid)
            {
                product.CreatedDate = DateTime.Now;
                product.IsActive = true;

                _context.Products.Add(product);
                await _context.SaveChangesAsync();  //   Async save

                TempData["SuccessMessage"] = "Product created successfully!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Categories = await _context.Categories
                .OrderBy(c => c.CategoryName)
                .ToListAsync();
            return View(product);
        }

        //   OPTIMIZED: Async Edit GET
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
                return NotFound();

            ViewBag.Categories = await _context.Categories
                .OrderBy(c => c.CategoryName)
                .ToListAsync();
            return View(product);
        }

        //   OPTIMIZED: Async Edit POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product)
        {
            if (id != product.ProductId)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(product);
                    await _context.SaveChangesAsync();
                    
                    TempData["SuccessMessage"] = "Product updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await ProductExistsAsync(product.ProductId))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Categories = await _context.Categories
                .OrderBy(c => c.CategoryName)
                .ToListAsync();
            return View(product);
        }

        //   OPTIMIZED: Async Delete GET
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
                return NotFound();

            return View(product);
        }

        //   OPTIMIZED: Async Delete POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Product deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        //   OPTIMIZED: Async Low Stock Report with server-side filtering and pagination
        public async Task<IActionResult> LowStockReport(int page = 1)
        {
            const int pageSize = 50;

            //   Server-side filtering - only low stock products
            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => p.StockQuantity <= p.ReorderLevel && p.IsActive);

            //   Get total count
            var totalProducts = await query.CountAsync();

            //   Get paginated results
            var lowStockProducts = await query
                .OrderBy(p => p.StockQuantity)
                .ThenBy(p => p.ProductName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            //   Pass pagination data
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalProducts / (double)pageSize);
            ViewBag.TotalProducts = totalProducts;

            return View(lowStockProducts);
        }

        //   NEW: AJAX endpoint for quick product search (autocomplete)
        [HttpGet]
        public async Task<JsonResult> SearchProducts(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return Json(new List<object>());

            var products = await _context.Products
                .Where(p => p.IsActive && p.ProductName.Contains(term))
                .OrderBy(p => p.ProductName)
                .Take(10)
                .Select(p => new 
                {
                    id = p.ProductId,
                    label = p.ProductName,
                    category = p.Category.CategoryName,
                    price = p.UnitPrice,
                    stock = p.StockQuantity
                })
                .ToListAsync();

            return Json(products);
        }

        //   NEW: AJAX endpoint for product details (for quick view)
        [HttpGet]
        public async Task<JsonResult> GetProductDetails(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.ProductId == id)
                .Select(p => new
                {
                    productId = p.ProductId,
                    productName = p.ProductName,
                    categoryId = p.CategoryId,
                    categoryName = p.Category.CategoryName,
                    unitPrice = p.UnitPrice,
                    stockQuantity = p.StockQuantity,
                    reorderLevel = p.ReorderLevel,
                    isActive = p.IsActive,
                    createdDate = p.CreatedDate.ToString("yyyy-MM-dd")
                })
                .FirstOrDefaultAsync();

            if (product == null)
                return Json(new { success = false, message = "Product not found" });

            return Json(new { success = true, data = product });
        }

        //   NEW: Get products by category (AJAX)
        [HttpGet]
        public async Task<JsonResult> GetProductsByCategory(int categoryId)
        {
            var products = await _context.Products
                .Where(p => p.CategoryId == categoryId && p.IsActive)
                .OrderBy(p => p.ProductName)
                .Select(p => new
                {
                    id = p.ProductId,
                    name = p.ProductName,
                    price = p.UnitPrice,
                    stock = p.StockQuantity
                })
                .ToListAsync();

            return Json(products);
        }

        //   OPTIMIZED: Async helper method
        private async Task<bool> ProductExistsAsync(int id)
        {
            return await _context.Products.AnyAsync(e => e.ProductId == id);
        }
    }
}