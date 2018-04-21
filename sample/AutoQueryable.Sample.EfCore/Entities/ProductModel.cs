﻿using System;
using System.Collections.Generic;

namespace AutoQueryable.Sample.EfCore.Entities
{
    public class ProductModel
    {
        public ProductModel()
        {
            this.Product = new HashSet<Product>();
        }

        public int ProductModelId { get; set; }
        public string Name { get; set; }
        public string CatalogDescription { get; set; }
        public Guid Rowguid { get; set; }
        public DateTime ModifiedDate { get; set; }

        public virtual IEnumerable<Product> Product { get; set; }
    }
}
