﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Ucommerce.Api;
using Ucommerce.Api.Search;
using UCommerce.Infrastructure;
using UCommerce.RazorStore.Models;
using UCommerce.Search.FacetsV2;
using UCommerce.Search.Models;
using Umbraco.Web.Mvc;

namespace Ucommerce.Avenue.Umbraco.Controllers
{
    public class RouterController : RenderMvcController
    {
        private ICatalogContext CatalogContext => ObjectFactory.Instance.Resolve<ICatalogContext>();
        private ICatalogLibrary CatalogLibrary => ObjectFactory.Instance.Resolve<ICatalogLibrary>();
        private ITransactionLibrary TransactionLibrary => ObjectFactory.Instance.Resolve<ITransactionLibrary>();
        public ISearchLibrary SearchLibrary => ObjectFactory.Instance.Resolve<ISearchLibrary>();


        public ActionResult Index()
        {
            var product = CatalogContext.CurrentProduct;
            if (product != null)
            {
                return RenderView(product);
            }

            var category = CatalogContext.CurrentCategory;
            if (category != null)
            {
                return RenderView(category);
            }

            return Content("Nåt Faunt");
        }


        protected virtual ActionResult RenderView(Product currentProduct, bool addedToBasket = false)
        {
            var productViewModel = new ProductViewModel
            {
                Sku = currentProduct.Sku,
                Name = currentProduct.DisplayName,
                LongDescription = currentProduct.LongDescription,
                IsOrderingAllowed = currentProduct.AllowOrdering,
                IsProductFamily = currentProduct.ProductFamily,
                IsVariant = false
            };

            // Price calculations
            var productGuids = new List<Guid>() {currentProduct.Guid};
            var productPriceCalculationResult = CatalogLibrary.CalculatePrices(productGuids);
            var productPriceCalculationResultItem = productPriceCalculationResult.Items.FirstOrDefault();
            if (productPriceCalculationResultItem != null)
            {
                productViewModel.TaxCalculation = productPriceCalculationResultItem.ListTax.ToString();
                productViewModel.PriceCalculation = new ProductPriceCalculationViewModel()
                {
                    YourPrice = productPriceCalculationResultItem.PriceInclTax.ToString("C"),
                    ListPrice = productPriceCalculationResultItem.ListPriceInclTax.ToString("C")
                };
            }

            if (!string.IsNullOrEmpty(currentProduct.PrimaryImageUrl))
            {
                productViewModel.ThumbnailImageUrl = currentProduct.PrimaryImageUrl;
            }

            // productViewModel.Properties = MapProductProperties(currentProduct);

            if (currentProduct.ProductFamily)
            {
                // TODO:
                // productViewModel.Variants = MapVariants(currentProduct.Variants);
            }

            bool isInBasket = TransactionLibrary.GetBasket(true).OrderLines.Any(x => x.Sku == currentProduct.Sku);

            var productPageViewModel = new ProductPageViewModel
            {
                ProductViewModel = productViewModel,
                AddedToBasket = addedToBasket,
                ItemAlreadyExists = isInBasket
            };

            return View("/Views/Product.cshtml", productPageViewModel);
        }

        protected virtual ActionResult RenderView(Category currentCategory)
        {
            var categoryViewModel = new CategoryViewModel
            {
                Name = currentCategory.DisplayName,
                Description = currentCategory.Description,
                CatalogId = currentCategory.ProductCatalog,
                CategoryId = currentCategory.Guid,
                Products = MapProductsInCategories(currentCategory)
            };

            if (!string.IsNullOrEmpty(currentCategory.ImageMediaUrl))
            {
                categoryViewModel.BannerImageUrl = currentCategory.ImageMediaUrl;
            }

            return View("/Views/Catalog.cshtml", categoryViewModel);
        }

        private IList<ProductViewModel> MapProducts(ICollection<Product> productsInCategory)
        {
            IList<ProductViewModel> productViews = new List<ProductViewModel>();

            foreach (var product in productsInCategory)
            {
                var productViewModel = new ProductViewModel
                {
                    Sku = product.Sku,
                    Name = product.Name,
                    ThumbnailImageUrl = product.ThumbnailImageUrl
                };


                productViews.Add(productViewModel);
            }

            return productViews;
        }

        private IList<ProductViewModel> MapProductsInCategories(Category category)
        {
            IList<Facet> facetsForQuerying = System.Web.HttpContext.Current.Request.QueryString.ToFacets();
            var productsInCategory = new List<ProductViewModel>();

            var subCategories = CatalogLibrary.GetCategories(category.Categories);
            var products = SearchLibrary.GetProductsFor(subCategories.Select(x => x.Guid).ToList(), facetsForQuerying);

            foreach (var subCategory in subCategories)
            {
                var productsInSubCategory = products.Where(p => p.Categories.Contains(subCategory.Guid));
                productsInCategory.AddRange(MapProducts(productsInSubCategory.ToList()));
            }

            productsInCategory.AddRange(MapProducts(SearchLibrary.GetProductsFor(category.Guid, facetsForQuerying)
                .Results));

            return productsInCategory;
        }
    }
}
