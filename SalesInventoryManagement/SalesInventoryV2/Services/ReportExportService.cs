using ClosedXML . Excel;

using DinkToPdf;
using DinkToPdf . Contracts;

using System;
using System . Collections . Generic;
using System . IO;
using System . Linq;

namespace SalesInventoryV2 . Services
{
      public class ReportExportService
      {
            private readonly IConverter _pdfConverter;

            public ReportExportService ( IConverter pdfConverter )
            {
                  _pdfConverter = pdfConverter;
            }

            /// <summary>
            /// Convert HTML string to PDF bytes
            /// </summary>
            public byte [ ] GeneratePdfFromHtml ( string htmlContent , string pageTitle = "Report" )
            {
                  var globalSettings = new GlobalSettings
                  {
                        ColorMode = ColorMode.Color,
                        Orientation = Orientation.Portrait,
                        PaperSize = PaperKind.A4,
                        Margins = new MarginSettings { Top = 10, Bottom = 10, Left = 10, Right = 10 },
                        DocumentTitle = pageTitle
                  };

                  var objectSettings = new ObjectSettings
                  {
                        PagesCount = true,
                        HtmlContent = htmlContent,
                        WebSettings = {
                     DefaultEncoding = "utf-8",
                     UserStyleSheet = null
                 },
                        HeaderSettings = {
                     FontSize = 9,
                     Right = "Page [page] of [toPage]",
                     Line = true
                 },
                        FooterSettings = {
                     FontSize = 9,
                     Center = $"Generated on {DateTime.Now:MMM dd, yyyy hh:mm tt}",
                     Line = true
                 }
                  };

                  var document = new HtmlToPdfDocument()
                  {
                        GlobalSettings = globalSettings,
                        Objects = { objectSettings }
                  };

                  return _pdfConverter . Convert ( document );
            }

            /// <summary>
            /// Generate Excel file from generic list
            /// </summary>
            public byte [ ] GenerateExcel<T> ( List<T> data , string sheetName = "Sheet1" , string title = null )
            {
                  using ( var workbook = new XLWorkbook ( ) )
                  {
                        var worksheet = workbook.Worksheets.Add(sheetName);

                        int currentRow = 1;

                        // Add title if provided
                        if ( !string . IsNullOrEmpty ( title ) )
                        {
                              worksheet . Cell ( currentRow , 1 ) . Value = title;
                              worksheet . Cell ( currentRow , 1 ) . Style . Font . Bold = true;
                              worksheet . Cell ( currentRow , 1 ) . Style . Font . FontSize = 16;
                              currentRow += 2;
                        }

                        // Insert data table
                        var table = worksheet.Cell(currentRow, 1).InsertTable(data);
                        table . Theme = XLTableTheme . TableStyleMedium2;

                        // Format header row
                        var headerRow = worksheet.Row(currentRow);
                        headerRow . Style . Font . Bold = true;
                        headerRow . Style . Fill . BackgroundColor = XLColor . FromHtml ( "#4472C4" );
                        headerRow . Style . Font . FontColor = XLColor . White;
                        headerRow . Style . Alignment . Horizontal = XLAlignmentHorizontalValues . Center;

                        // Auto-fit columns
                        worksheet . Columns ( ) . AdjustToContents ( );

                        using ( var stream = new MemoryStream ( ) )
                        {
                              workbook . SaveAs ( stream );
                              return stream . ToArray ( );
                        }
                  }
            }

            /// <summary>
            /// Generate Excel with multiple sheets
            /// </summary>
            public byte [ ] GenerateExcelMultiSheet ( Dictionary<string , object> sheets , string reportTitle = null )
            {
                  using ( var workbook = new XLWorkbook ( ) )
                  {
                        foreach ( var sheet in sheets )
                        {
                              var worksheet = workbook.Worksheets.Add(sheet.Key);
                              int currentRow = 1;

                              // Add title if provided
                              if ( !string . IsNullOrEmpty ( reportTitle ) )
                              {
                                    worksheet . Cell ( currentRow , 1 ) . Value = reportTitle;
                                    worksheet . Cell ( currentRow , 1 ) . Style . Font . Bold = true;
                                    worksheet . Cell ( currentRow , 1 ) . Style . Font . FontSize = 16;
                                    currentRow += 2;
                              }

                              // Insert data
                              var dataType = sheet.Value.GetType();
                              if ( dataType . IsGenericType && dataType . GetGenericTypeDefinition ( ) == typeof ( List<> ) )
                              {
                                    var method = typeof(IXLWorksheet).GetMethod("InsertTable");
                                    var genericMethod = method.MakeGenericMethod(dataType.GetGenericArguments()[0]);
                                    var table = (IXLTable)genericMethod.Invoke(worksheet.Cell(currentRow, 1), new[] { sheet.Value });
                                    table . Theme = XLTableTheme . TableStyleMedium2;
                              }

                              // Format and auto-fit
                              worksheet . Columns ( ) . AdjustToContents ( );
                        }

                        using ( var stream = new MemoryStream ( ) )
                        {
                              workbook . SaveAs ( stream );
                              return stream . ToArray ( );
                        }
                  }
            }

            /// <summary>
            /// Generate Excel with custom formatting
            /// </summary>
            public byte [ ] GenerateFormattedExcel<T> (
                List<T> data ,
                string sheetName ,
                string title ,
                Dictionary<string , string> columnHeaders = null ,
                List<string> currencyColumns = null )
            {
                  using ( var workbook = new XLWorkbook ( ) )
                  {
                        var worksheet = workbook.Worksheets.Add(sheetName);
                        int currentRow = 1;

                        // Add title
                        if ( !string . IsNullOrEmpty ( title ) )
                        {
                              var titleCell = worksheet.Cell(currentRow, 1);
                              titleCell . Value = title;
                              titleCell . Style . Font . Bold = true;
                              titleCell . Style . Font . FontSize = 18;
                              titleCell . Style . Font . FontColor = XLColor . FromHtml ( "#1F4E78" );
                              currentRow += 2;
                        }

                        // Add generation date
                        worksheet . Cell ( currentRow , 1 ) . Value = $"Generated: {DateTime . Now:MMM dd, yyyy hh:mm tt}";
                        worksheet . Cell ( currentRow , 1 ) . Style . Font . Italic = true;
                        worksheet . Cell ( currentRow , 1 ) . Style . Font . FontSize = 10;
                        currentRow += 2;

                        // Insert table
                        var table = worksheet.Cell(currentRow, 1).InsertTable(data);
                        table . Theme = XLTableTheme . TableStyleLight9;

                        // Format header
                        var headerRow = worksheet.Row(currentRow);
                        headerRow . Style . Font . Bold = true;
                        headerRow . Style . Fill . BackgroundColor = XLColor . FromHtml ( "#4472C4" );
                        headerRow . Style . Font . FontColor = XLColor . White;
                        headerRow . Style . Alignment . Horizontal = XLAlignmentHorizontalValues . Center;

                        // Apply custom column headers if provided
                        if ( columnHeaders != null )
                        {
                              int col = 1;
                              foreach ( var header in columnHeaders )
                              {
                                    worksheet . Cell ( currentRow , col ) . Value = header . Value;
                                    col++;
                              }
                        }

                        // Format currency columns
                        if ( currencyColumns != null && currencyColumns . Any ( ) )
                        {
                              var properties = typeof(T).GetProperties();
                              for ( int i = 0 ; i < properties . Length ; i++ )
                              {
                                    if ( currencyColumns . Contains ( properties [ i ] . Name ) )
                                    {
                                          var column = worksheet.Column(i + 1);
                                          column . Style . NumberFormat . Format = "$#,##0.00";
                                          column . Style . Alignment . Horizontal = XLAlignmentHorizontalValues . Right;
                                    }
                              }
                        }

                        // Auto-fit columns
                        worksheet . Columns ( ) . AdjustToContents ( );

                        // Set minimum column width
                        foreach ( var column in worksheet . ColumnsUsed ( ) )
                        {
                              if ( column . Width < 15 )
                                    column . Width = 15;
                        }

                        using ( var stream = new MemoryStream ( ) )
                        {
                              workbook . SaveAs ( stream );
                              return stream . ToArray ( );
                        }
                  }
            }

            /// <summary>
            /// Generate CSV from data
            /// </summary>
            public byte [ ] GenerateCsv<T> ( List<T> data )
            {
                  var properties = typeof(T).GetProperties();
                  var csv = new System.Text.StringBuilder();

                  // Header
                  csv . AppendLine ( string . Join ( "," , properties . Select ( p => p . Name ) ) );

                  // Data
                  foreach ( var item in data )
                  {
                        var values = properties.Select(p =>
                 {
                       var value = p.GetValue(item)?.ToString() ?? "";
                       // Escape commas and quotes
                       if (value.Contains(",") || value.Contains("\""))
                             value = $"\"{value.Replace("\"", "\"\"")}\"";
                       return value;
                 });
                        csv . AppendLine ( string . Join ( "," , values ) );
                  }

                  return System . Text . Encoding . UTF8 . GetBytes ( csv . ToString ( ) );
            }
      }
}