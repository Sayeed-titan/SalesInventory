// Services/ReportExportService.cs
// FIXED VERSION: Handles null references and generic type issues

using ClosedXML . Excel;
using Microsoft . Extensions . Logging;
using System;
using System . Collections . Generic;
using System . IO;
using System . Linq;
using System . Reflection;
using IronPdf;

namespace SalesInventoryV2 . Services
{
      public class ReportExportService
      {
            private readonly ILogger<ReportExportService> _logger;

            public ReportExportService ( ILogger<ReportExportService> logger )
            {
                  _logger = logger;
            }

            /// <summary>
            /// Generate PDF from HTML using IronPDF
            /// </summary>
            public byte [ ] GeneratePdfFromHtml ( string htmlContent , string title )
            {
                  try
                  {
                        var renderer = new ChromePdfRenderer();

                        // Configure rendering options
                        renderer . RenderingOptions . MarginTop = 20;
                        renderer . RenderingOptions . MarginBottom = 20;
                        renderer . RenderingOptions . MarginLeft = 20;
                        renderer . RenderingOptions . MarginRight = 20;
                        renderer . RenderingOptions . PrintHtmlBackgrounds = true;
                        renderer . RenderingOptions . PaperSize = IronPdf . Rendering . PdfPaperSize . A4;

                        // Generate PDF
                        var pdf = renderer.RenderHtmlAsPdf(htmlContent);

                        return pdf . BinaryData;
                  }
                  catch ( Exception ex )
                  {
                        _logger . LogError ( ex , "Error generating PDF" );
                        throw;
                  }
            }

            /// <summary>
            /// Generate formatted Excel from a collection of objects
            /// </summary>
            public byte [ ] GenerateFormattedExcel<T> (
                IEnumerable<T> data ,
                string sheetName ,
                string title = null ,
                List<string> currencyColumns = null )
            {
                  try
                  {
                        using ( var workbook = new XLWorkbook ( ) )
                        {
                              var worksheet = workbook.Worksheets.Add(sheetName);
                              int currentRow = 1;

                              // Add title if provided
                              if ( !string . IsNullOrEmpty ( title ) )
                              {
                                    worksheet . Cell ( currentRow , 1 ) . Value = title;
                                    worksheet . Cell ( currentRow , 1 ) . Style
                                        . Font . SetBold ( true )
                                        . Font . SetFontSize ( 16 )
                                        . Fill . SetBackgroundColor ( XLColor . LightBlue );

                                    var properties = typeof(T).GetProperties();
                                    worksheet . Range ( currentRow , 1 , currentRow , properties . Length ) . Merge ( );
                                    currentRow += 2;
                              }

                              // Add headers
                              var props = typeof(T).GetProperties();
                              for ( int i = 0 ; i < props . Length ; i++ )
                              {
                                    var headerCell = worksheet.Cell(currentRow, i + 1);
                                    headerCell . Value = props [ i ] . Name;
                                    headerCell . Style
                                        . Font . SetBold ( true )
                                        . Fill . SetBackgroundColor ( XLColor . DarkBlue )
                                        . Font . SetFontColor ( XLColor . White );
                              }
                              currentRow++;

                              // Add data
                              var dataList = data.ToList();
                              foreach ( var item in dataList )
                              {
                                    for ( int i = 0 ; i < props . Length ; i++ )
                                    {
                                          var value = props[i].GetValue(item);
                                          var cell = worksheet.Cell(currentRow, i + 1);

                                          if ( value != null )
                                          {
                                                cell . Value = value . ToString ( );

                                                // Format currency columns
                                                if ( currencyColumns != null &&
                                                    currencyColumns . Contains ( props [ i ] . Name ) &&
                                                    decimal . TryParse ( value . ToString ( ) , out decimal decimalValue ) )
                                                {
                                                      cell . Value = decimalValue;
                                                      cell . Style . NumberFormat . Format = "$#,##0.00";
                                                }
                                          }
                                    }
                                    currentRow++;
                              }

                              // Auto-fit columns
                              worksheet . Columns ( ) . AdjustToContents ( );

                              using ( var stream = new MemoryStream ( ) )
                              {
                                    workbook . SaveAs ( stream );
                                    return stream . ToArray ( );
                              }
                        }
                  }
                  catch ( Exception ex )
                  {
                        _logger . LogError ( ex , "Error generating formatted Excel" );
                        throw;
                  }
            }

            /// <summary>
            /// Generate Excel with multiple sheets - FIXED VERSION
            /// </summary>
            public byte [ ] GenerateExcelMultiSheet (
                Dictionary<string , object> sheets ,
                string reportTitle = null )
            {
                  try
                  {
                        if ( sheets == null || sheets . Count == 0 )
                        {
                              throw new ArgumentException ( "Sheets dictionary cannot be null or empty" , nameof ( sheets ) );
                        }

                        using ( var workbook = new XLWorkbook ( ) )
                        {
                              foreach ( var sheet in sheets )
                              {
                                    if ( sheet . Value == null )
                                    {
                                          _logger . LogWarning ( $"Sheet '{sheet . Key}' has null data, skipping" );
                                          continue;
                                    }

                                    var sheetName = SanitizeSheetName(sheet.Key);
                                    var worksheet = workbook.Worksheets.Add(sheetName);

                                    // Add title if this is the first sheet
                                    int currentRow = 1;
                                    if ( !string . IsNullOrEmpty ( reportTitle ) && sheets . Keys . First ( ) == sheet . Key )
                                    {
                                          worksheet . Cell ( currentRow , 1 ) . Value = reportTitle;
                                          worksheet . Cell ( currentRow , 1 ) . Style
                                              . Font . SetBold ( true )
                                              . Font . SetFontSize ( 16 )
                                              . Fill . SetBackgroundColor ( XLColor . LightBlue );
                                          worksheet . Range ( currentRow , 1 , currentRow , 10 ) . Merge ( );
                                          currentRow += 2;
                                    }

                                    // Handle the data based on its type
                                    var dataType = sheet.Value.GetType();

                                    if ( dataType . IsGenericType &&
                                        dataType . GetGenericTypeDefinition ( ) == typeof ( List<> ) )
                                    {
                                          // It's a List<T>
                                          var elementType = dataType.GetGenericArguments()[0];
                                          var listData = sheet.Value as System.Collections.IEnumerable;

                                          if ( listData != null )
                                          {
                                                PopulateWorksheetFromList ( worksheet , listData , elementType , currentRow );
                                          }
                                    }
                                    else if ( sheet . Value is System . Collections . IEnumerable enumerable &&
                                             !( sheet . Value is string ) )
                                    {
                                          // It's an IEnumerable but not a string
                                          var firstItem = enumerable.Cast<object>().FirstOrDefault();
                                          if ( firstItem != null )
                                          {
                                                var elementType = firstItem.GetType();
                                                PopulateWorksheetFromList ( worksheet , enumerable , elementType , currentRow );
                                          }
                                    }
                                    else
                                    {
                                          // Single object or unknown type
                                          _logger . LogWarning ( $"Sheet '{sheet . Key}' has unsupported data type: {dataType . Name}" );
                                    }

                                    // Auto-fit columns
                                    worksheet . Columns ( ) . AdjustToContents ( );
                              }

                              if ( workbook . Worksheets . Count == 0 )
                              {
                                    throw new InvalidOperationException ( "No valid worksheets were created" );
                              }

                              using ( var stream = new MemoryStream ( ) )
                              {
                                    workbook . SaveAs ( stream );
                                    return stream . ToArray ( );
                              }
                        }
                  }
                  catch ( Exception ex )
                  {
                        _logger . LogError ( ex , "Error generating multi-sheet Excel" );
                        throw;
                  }
            }

            /// <summary>
            /// Helper method to populate worksheet from a list
            /// </summary>
            private void PopulateWorksheetFromList (
                IXLWorksheet worksheet ,
                System . Collections . IEnumerable data ,
                Type elementType ,
                int startRow )
            {
                  try
                  {
                        var properties = elementType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                        if ( properties == null || properties . Length == 0 )
                        {
                              _logger . LogWarning ( $"No properties found for type {elementType . Name}" );
                              return;
                        }

                        int currentRow = startRow;

                        // Add headers
                        for ( int i = 0 ; i < properties . Length ; i++ )
                        {
                              var headerCell = worksheet.Cell(currentRow, i + 1);
                              headerCell . Value = properties [ i ] . Name;
                              headerCell . Style
                                  . Font . SetBold ( true )
                                  . Fill . SetBackgroundColor ( XLColor . DarkBlue )
                                  . Font . SetFontColor ( XLColor . White );
                        }
                        currentRow++;

                        // Add data rows
                        var dataList = data.Cast<object>().ToList();
                        foreach ( var item in dataList )
                        {
                              if ( item == null ) continue;

                              for ( int i = 0 ; i < properties . Length ; i++ )
                              {
                                    try
                                    {
                                          var value = properties[i].GetValue(item);
                                          var cell = worksheet.Cell(currentRow, i + 1);

                                          if ( value != null )
                                          {
                                                // Handle different data types
                                                if ( value is DateTime dateTime )
                                                {
                                                      cell . Value = dateTime;
                                                      cell . Style . DateFormat . Format = "MM/dd/yyyy";
                                                }
                                                else if ( value is decimal || value is double || value is float )
                                                {
                                                      if ( decimal . TryParse ( value . ToString ( ) , out decimal decimalValue ) )
                                                      {
                                                            cell . Value = decimalValue;

                                                            // Check if it might be currency based on property name
                                                            var propName = properties[i].Name.ToLower();
                                                            if ( propName . Contains ( "price" ) ||
                                                                propName . Contains ( "amount" ) ||
                                                                propName . Contains ( "revenue" ) ||
                                                                propName . Contains ( "spent" ) ||
                                                                propName . Contains ( "cost" ) )
                                                            {
                                                                  cell . Style . NumberFormat . Format = "$#,##0.00";
                                                            }
                                                            else
                                                            {
                                                                  cell . Style . NumberFormat . Format = "#,##0.00";
                                                            }
                                                      }
                                                }
                                                else if ( value is int || value is long )
                                                {
                                                      cell . Value = Convert . ToInt64 ( value );
                                                      cell . Style . NumberFormat . Format = "#,##0";
                                                }
                                                else
                                                {
                                                      cell . Value = value . ToString ( );
                                                }
                                          }
                                    }
                                    catch ( Exception ex )
                                    {
                                          _logger . LogError ( ex , $"Error setting cell value for property {properties [ i ] . Name}" );
                                          worksheet . Cell ( currentRow , i + 1 ) . Value = "Error";
                                    }
                              }
                              currentRow++;
                        }
                  }
                  catch ( Exception ex )
                  {
                        _logger . LogError ( ex , "Error populating worksheet from list" );
                        throw;
                  }
            }

            /// <summary>
            /// Sanitize sheet name for Excel
            /// </summary>
            private string SanitizeSheetName ( string name )
            {
                  if ( string . IsNullOrEmpty ( name ) )
                        return "Sheet1";

                  // Remove invalid characters
                  var invalidChars = new[] { '\\', '/', '*', '?', ':', '[', ']' };
                  foreach ( var c in invalidChars )
                  {
                        name = name . Replace ( c . ToString ( ) , "" );
                  }

                  // Limit length to 31 characters (Excel limit)
                  if ( name . Length > 31 )
                        name = name . Substring ( 0 , 31 );

                  return name;
            }
      }
}