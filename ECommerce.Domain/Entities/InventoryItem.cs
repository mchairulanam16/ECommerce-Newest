using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Domain.Entities
{
    public class InventoryItem
    {
        public string Sku { get; private set; } = default!;
        public int ActualQty { get; private set; }
        public int ReservedQty { get; private set; }

        public byte[] RowVersion { get; private set; } = default!;

        private InventoryItem() { }

        public InventoryItem(string sku, int actualQty, byte[]? rowVersion = null)
        {
            Sku = sku;
            ActualQty = actualQty;
            //RowVersion = rowVersion ?? BitConverter.GetBytes(1L);
        }

        public void Reserve(int qty)
        {
            if (ActualQty - ReservedQty < qty)
                throw new InvalidOperationException($"Insufficient stock for {Sku}");

            ReservedQty += qty;
        }

        public void Commit(int qty)
        {
            if (ReservedQty < qty)
                throw new InvalidOperationException("Invalid commit");

            ReservedQty -= qty;
            ActualQty -= qty;
        }

        public void Release(int qty)
        {
            if (ReservedQty < qty)
                throw new InvalidOperationException("Invalid release");

            ReservedQty -= qty;
        }
    }
}
