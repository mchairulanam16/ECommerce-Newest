using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Domain.Entities
{
    public class OrderItem
    {
        public Guid OrderId { get; private set; }
        public string Sku { get; private set; } = default!;
        public int Qty { get; private set; }

        private OrderItem() { }

        internal OrderItem(Guid orderId, string sku, int qty)
        {
            if (qty <= 0)
                throw new ArgumentException("Quantity must be positive"); 

            OrderId = orderId;
            Sku = sku;
            Qty = qty;
        }
    }

}
