using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xero.NetStandard.OAuth2.Api;
using Xero.NetStandard.OAuth2.Config;
using Xero.NetStandard.OAuth2.Model.Accounting;

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
}