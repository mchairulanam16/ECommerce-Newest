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

        public static InventoryItem CreateForTest(string sku, int actualQty, int reservedQty = 0)
        {
            return new InventoryItem
            {
                Sku = sku,
                ActualQty = actualQty,
                ReservedQty = reservedQty,
                RowVersion = new byte[8] // Default for test
            };
        }
    }
}
