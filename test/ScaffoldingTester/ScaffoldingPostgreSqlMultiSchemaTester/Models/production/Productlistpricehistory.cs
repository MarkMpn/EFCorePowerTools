﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;
using ScaffoldingPostgreSqlMultiSchemaTester.Models.dboSchema;
using ScaffoldingPostgreSqlMultiSchemaTester.Models.HumanResourcesSchema;
using ScaffoldingPostgreSqlMultiSchemaTester.Models.PersonSchema;
using ScaffoldingPostgreSqlMultiSchemaTester.Models.ProductionSchema;
using ScaffoldingPostgreSqlMultiSchemaTester.Models.PurchasingSchema;
using ScaffoldingPostgreSqlMultiSchemaTester.Models.SalesSchema;


namespace ScaffoldingPostgreSqlMultiSchemaTester.Models.ProductionSchema
{
    /// <summary>
    /// Changes in the list price of a product over time.
    /// </summary>
    public partial class ProductListPriceHistory
    {
        /// <summary>
        /// Product identification number. Foreign key to Product.ProductID
        /// </summary>
        public int ProductId { get; set; }
        /// <summary>
        /// List price start date.
        /// </summary>
        public DateTime StartDate { get; set; }
        /// <summary>
        /// List price end date
        /// </summary>
        public DateTime? EndDate { get; set; }
        /// <summary>
        /// Product list price.
        /// </summary>
        public decimal ListPrice { get; set; }
        /// <summary>
        /// Date and time the record was last updated.
        /// </summary>
        public DateTime ModifiedDate { get; set; }

        public virtual Product Product { get; set; }
    }
}