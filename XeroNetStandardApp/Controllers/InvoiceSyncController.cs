using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xero.NetStandard.OAuth2.Api;
using Xero.NetStandard.OAuth2.Config;
using Xero.NetStandard.OAuth2.Model.Accounting;
using XeroNetStandardApp.Services;

namespace XeroNetStandardApp.Controllers
{
    public class InvoiceSync : ApiAccessorController<AccountingApi>
    {
        public InvoiceSync(IOptions<XeroConfiguration> xeroConfig) : base(xeroConfig) { }

        // GET: /InvoiceSync/
        public async Task<IActionResult> Index()
        {
            var sevenDaysAgo = DateTime.Now.AddDays(-7).ToString("yyyy, MM, dd");
            var invoicesFilter = "Date >= DateTime(" + sevenDaysAgo + ")"; // "Date >= DateTime(" + sevenDaysAgo + ")"; // "InvoiceNumber==\"INV-0031\"";

            var response = await Api.GetInvoicesAsync(XeroToken.AccessToken, TenantId, where: invoicesFilter);

            return View(response._Invoices);
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var response = await Api.GetInvoiceAsync(XeroToken.AccessToken, TenantId, id);

            var invoice = response._Invoices?.FirstOrDefault();

            if (invoice == null)
            {
                return NotFound($"Invoice with ID {id} not found.");
            }

            // NEW to INCLUDE Geocoding: pick an address from the contact
            var geocodedAddresses = new List<GeocodedAddressResult>();

            if (invoice.Contact?.Addresses != null && invoice.Contact.Addresses.Any())
            {
                using var geocoder = new OsmGeocoder();

                foreach (var addr in invoice.Contact.Addresses)
                {
                    var latLon = await geocoder.GeocodeXeroAddressAsync(addr); // "LAT,LON" or null

                    if (!string.IsNullOrWhiteSpace(latLon))
                    {
                        geocodedAddresses.Add(new GeocodedAddressResult
                        {
                            Address = addr,
                            LatLon = latLon
                        });
                    }
                }
            }

            // You can still pass the raw invoice as the model. // Pass the LAT,LON to the view, without changing the model type.
            // The view gets an extra value: ViewBag.GeoEncodedAddresses that coincides with the actual returned address (a simple string like "13.7563,100.5018")
            ViewBag.GeocodedAddresses = geocodedAddresses;

            return View(invoice);
        }

        // GET: /InvoiceSync#Create
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // POST: /InvoiceSync#Create
        [HttpPost]
        public async Task<IActionResult> Create(string name, string lineDescription, string lineQuantity, string lineUnitAmount, string lineAccountCode)
        {
            var contact = new Contact { Name = name };

            var line = new LineItem
            {
                Description = lineDescription,
                Quantity = decimal.Parse(lineQuantity),
                UnitAmount = decimal.Parse(lineUnitAmount),
                AccountCode = lineAccountCode
            };
            var lines = new List<LineItem> { line };

            var invoice = new Invoice
            {
                Type = Invoice.TypeEnum.ACCREC,
                Contact = contact,
                Date = DateTime.Today,
                DueDate = DateTime.Today.AddDays(30),
                LineItems = lines
            };

            var invoices = new Invoices
            {
                _Invoices = new List<Invoice> { invoice }
            };

            await Api.CreateInvoicesAsync(XeroToken.AccessToken, TenantId, invoices);

            return RedirectToAction("Index", "InvoiceSync");
        }
    }

    
    public static class XeroAddressHelper // Helper: flatten Xero address → single line
    {
        public static string NormalizeForGeocode(Address addr)
        {
            var parts = new[]
            {
                Clean(addr.AddressLine1),
                Clean(addr.AddressLine2),
                Clean(addr.AddressLine3),
                Clean(addr.City),
                Clean(addr.Region),
                Clean(addr.PostalCode),
                NormalizeCountry(addr.Country)
            }
            .Where(p => !string.IsNullOrWhiteSpace(p));

            return string.Join(", ", parts);
        }

        private static string Clean(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;

            // collapse multiple spaces
            return Regex.Replace(s.Trim(), @"\s+", " ");
        }

        private static string NormalizeCountry(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            raw = raw.Trim();

            // try direct match
            if (CountryMap.TryGetValue(raw, out var normalized))
                return normalized;

            // try uppercase match
            var upper = raw.ToUpperInvariant();
            if (CountryMap.TryGetValue(upper, out var normalizedUpper))
                return normalizedUpper;

            // return unchanged if no mapping found
            return raw;
        }

        private static readonly Dictionary<string, string> CountryMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // --- North America ---
            { "CA", "Canada" },
            { "CAN", "Canada" },
            { "CDN", "Canada" },

            { "US", "United States" },
            { "USA", "United States" },
            { "U.S.A", "United States" },
            { "UNITED STATES OF AMERICA", "United States" },

            // --- Oceania (common in Xero) ---
            { "AU", "Australia" },
            { "AUS", "Australia" },

            { "NZ", "New Zealand" },
            { "NZL", "New Zealand" },

            // --- ASEAN (you will use these a lot) ---
            { "SG", "Singapore" },
            { "SIN", "Singapore" },

            { "MY", "Malaysia" },
            { "MYS", "Malaysia" },

            { "TH", "Thailand" },
            { "THA", "Thailand" },

            { "ID", "Indonesia" },
            { "IDN", "Indonesia" },

            { "PH", "Philippines" },
            { "PHL", "Philippines" },

            { "VN", "Vietnam" },
            { "VNM", "Vietnam" },

            { "KH", "Cambodia" },
            { "KHM", "Cambodia" },

            { "BN", "Brunei Darussalam" },
            { "BRN", "Brunei Darussalam" },

            { "MM", "Myanmar" },
            { "MMR", "Myanmar" },

            { "LA", "Laos" },
            { "LAO", "Laos" },

            // --- East Asia ---
            { "CN", "China" },
            { "CHN", "China" },

            { "HK", "Hong Kong" },
            { "HKG", "Hong Kong" },

            { "TW", "Taiwan" },
            { "TWN", "Taiwan" },

            { "KR", "South Korea" },
            { "KOR", "South Korea" },

            { "JP", "Japan" },
            { "JPN", "Japan" },
        };
    }
}