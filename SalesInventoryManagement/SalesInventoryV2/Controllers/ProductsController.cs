using Microsoft . AspNetCore . Mvc;
using Microsoft . EntityFrameworkCore;

using SalesInventoryV2 . Data;
using SalesInventoryV2 . Models;

using System;
using System . Linq;

namespace SalesInventoryV1 . Controllers
{
      public class ProductsController : Controller
      {
            private readonly ApplicationDbContext _context;

            public ProductsController ( ApplicationDbContext context )
            {
                  _context = context;
            }

            // GET: Products/Index
            public IActionResult Index ( string searchTerm , int? categoryId )
            {
                  // Load products with Category - NO stored procedure
                  var products = _context.Products
                .Include(p => p.Category)
                .ToList();

                  // Client-side filtering
                  if ( !string . IsNullOrEmpty ( searchTerm ) )
                  {
                        products = products
                            . Where ( p => p . ProductName . Contains ( searchTerm , StringComparison . OrdinalIgnoreCase ) )
                            . ToList ( );
                  }

                  if ( categoryId . HasValue )
                  {
                        products = products . Where ( p => p . CategoryId == categoryId . Value ) . ToList ( );
                  }

                  ViewBag . Categories = _context . Categories . ToList ( );
                  ViewBag . SearchTerm = searchTerm;
                  ViewBag . CategoryId = categoryId;

                  return View ( products );
            }

            // GET: Products/Details/5
            public IActionResult Details ( int id )
            {
                  var product = _context.Products
                .Include(p => p.Category)
                .FirstOrDefault(p => p.ProductId == id);

                  if ( product == null )
                        return NotFound ( );

                  return View ( product );
            }

            // GET: Products/Create
            public IActionResult Create ( )
            {
                  ViewBag . Categories = _context . Categories . ToList ( );
                  return View ( );
            }

            // POST: Products/Create
            [HttpPost]
            [ValidateAntiForgeryToken]
            public IActionResult Create ( Product product )
            {
                  if ( ModelState . IsValid )
                  {
                        product . CreatedDate = DateTime . Now;
                        product . IsActive = true;

                        _context . Products . Add ( product );
                        _context . SaveChanges ( );

                        TempData [ "SuccessMessage" ] = "Product created successfully!";
                        return RedirectToAction ( nameof ( Index ) );
                  }

                  ViewBag . Categories = _context . Categories . ToList ( );
                  return View ( product );
            }

            // GET: Products/Edit/5
            public IActionResult Edit ( int id )
            {
                  var product = _context.Products.Find(id);

                  if ( product == null )
                        return NotFound ( );

                  ViewBag . Categories = _context . Categories . ToList ( );
                  return View ( product );
            }

            // POST: Products/Edit/5
            [HttpPost]
            [ValidateAntiForgeryToken]
            public IActionResult Edit ( int id , Product product )
            {
                  if ( id != product . ProductId )
                        return NotFound ( );

                  if ( ModelState . IsValid )
                  {
                        try
                        {
                              _context . Update ( product );
                              _context . SaveChanges ( );

                              TempData [ "SuccessMessage" ] = "Product updated successfully!";
                        }
                        catch ( DbUpdateConcurrencyException )
                        {
                              if ( !ProductExists ( product . ProductId ) )
                                    return NotFound ( );
                              else
                                    throw;
                        }
                        return RedirectToAction ( nameof ( Index ) );
                  }

                  ViewBag . Categories = _context . Categories . ToList ( );
                  return View ( product );
            }

            // GET: Products/Delete/5
            public IActionResult Delete ( int id )
            {
                  var product = _context.Products
                .Include(p => p.Category)
                .FirstOrDefault(p => p.ProductId == id);

                  if ( product == null )
                        return NotFound ( );

                  return View ( product );
            }

            // POST: Products/Delete/5
            [HttpPost, ActionName ( "Delete" )]
            [ValidateAntiForgeryToken]
            public IActionResult DeleteConfirmed ( int id )
            {
                  var product = _context.Products.Find(id);
                  if ( product != null )
                  {
                        _context . Products . Remove ( product );
                        _context . SaveChanges ( );

                        TempData [ "SuccessMessage" ] = "Product deleted successfully!";
                  }

                  return RedirectToAction ( nameof ( Index ) );
            }

            // GET: Products/LowStockReport
            public IActionResult LowStockReport ( )
            {
                  var products = _context.Products
                .Include(p => p.Category)
                .ToList();

                  var lowStockProducts = products
                .Where(p => p.StockQuantity <= p.ReorderLevel && p.IsActive)
                .OrderBy(p => p.StockQuantity)
                .ToList();

                  return View ( lowStockProducts );
            }

            private bool ProductExists ( int id )
            {
                  return _context . Products . Any ( e => e . ProductId == id );
            }
      }
}