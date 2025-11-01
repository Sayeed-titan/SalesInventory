using Microsoft . AspNetCore . Mvc;
using Microsoft . EntityFrameworkCore;

using SalesInventoryV1 . Data;

namespace SalesInvetoryV1 . Controllers
{
      public class ProductsController : Controller
      {
            private readonly ApplicationDbContext _context;

            public ProductsController (ApplicationDbContext context )
            {
                _context = context;
            }

            public IActionResult Index (string searchTerm, int? categoryId )
            {
                  //Load all products with related Category
                  var products = _context.Products
                        .Include(p => p.Category) //Eager loading of Category
                        .ToList();

                  //Client -side filtering
                  if(!string.IsNullOrEmpty(searchTerm))
                  {
                        products = products
                              . Where ( p => p . ProductName 
                              . Contains ( searchTerm , StringComparison . OrdinalIgnoreCase ) )
                              . ToList ( );
                  }

                  if ( categoryId . HasValue )
                  {
                        products = products
                              .Where( p => p.CategoryId == categoryId.Value)
                              .ToList();
                  }

                  return View ( products );
            }



      }
}
