﻿using System.Linq;
using System.Web.Mvc;
using UCommerce;
using Ucommerce.Api;
using UCommerce.EntitiesV2;
using UCommerce.Infrastructure;
using UCommerce.RazorStore.Models;
using UCommerce.Search;
using Umbraco.Web;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Ucommerce.Avenue.Umbraco.Controllers
{
	public class BasketController : RenderMvcController
	{
		public ICatalogLibrary CatalogLibrary => ObjectFactory.Instance.Resolve<ICatalogLibrary>();
		public ICatalogContext CatalogContext => ObjectFactory.Instance.Resolve<ICatalogContext>();
		public ITransactionLibrary TransactionLibrary => ObjectFactory.Instance.Resolve<ITransactionLibrary>();
		public IUrlService UrlService => ObjectFactory.Instance.Resolve<IUrlService>();
	    
        [HttpGet]
		public override ActionResult Index(ContentModel model)
		{
			PurchaseOrder basket = TransactionLibrary.GetBasket();
			var basketModel = new PurchaseOrderViewModel();

			foreach (var orderLine in basket.OrderLines)
			{
			    var orderLineViewModel = new OrderlineViewModel
			    {
			        Quantity = orderLine.Quantity,
			        ProductName = orderLine.ProductName,
			        Sku = orderLine.Sku,
			        VariantSku = orderLine.VariantSku,
			        Total = new Money(orderLine.Total.GetValueOrDefault(), basket.BillingCurrency).ToString(),
			        Discount = orderLine.Discount,
			        Tax = new Money(orderLine.VAT, basket.BillingCurrency).ToString(),
			        Price = new Money(orderLine.Price, basket.BillingCurrency).ToString(),
			        ProductUrl = UrlService.GetUrl(CatalogContext.CurrentCatalog, new[] { CatalogLibrary.GetProduct(orderLine.Sku) }),
			        PriceWithDiscount = new Money(orderLine.Price - orderLine.UnitDiscount.GetValueOrDefault(), basket.BillingCurrency).ToString(),
			        OrderLineId = orderLine.OrderLineId
			    };


			    basketModel.OrderLines.Add(orderLineViewModel);
			}

            basketModel.OrderTotal = new Money(basket.OrderTotal.GetValueOrDefault(), basket.BillingCurrency).ToString();
            basketModel.DiscountTotal = new Money(basket.DiscountTotal.GetValueOrDefault(), basket.BillingCurrency).ToString();
            basketModel.TaxTotal =  new Money(basket.TaxTotal.GetValueOrDefault(), basket.BillingCurrency).ToString();
            basketModel.SubTotal = new Money(basket.SubTotal.GetValueOrDefault(), basket.BillingCurrency).ToString();
		   
		    return View("/Views/Basket.cshtml", basketModel);
		}

        [HttpPost]
        public ActionResult Index(PurchaseOrderViewModel model)
        {
            foreach (var orderLine in model.OrderLines)
            {
                var newQuantity = orderLine.Quantity;

                if (model.RemoveOrderlineId == orderLine.OrderLineId)
                    newQuantity = 0;

                TransactionLibrary.UpdateLineItem(orderLine.OrderLineId, newQuantity);
            }

            TransactionLibrary.ExecuteBasketPipeline();

            var shop = model.Content.AncestorsOrSelf().FirstOrDefault(x => x.ContentType.Alias.Equals("home"));
            var basket = shop.DescendantsOrSelf().FirstOrDefault(x => x.ContentType.Alias.Equals("basket"));
            return Redirect(basket.Url);
        }
    }

}