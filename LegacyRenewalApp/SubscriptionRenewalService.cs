using System;

namespace LegacyRenewalApp
{
    public class SubscriptionRenewalService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly ISubscriptionPlanRepository _planRepository;
        private readonly IBillingGateway _billingGateway;
        private readonly IPricingCalculator _pricingCalculator;

        // 1. STARY KONSTRUKTOR: Zachowuje kompatybilność z LegacyRenewalAppConsumer!
        // Używa naszych nowych klas domyślnie.
        public SubscriptionRenewalService() : this(
            new CustomerRepository(), 
            new SubscriptionPlanRepository(), 
            new BillingGatewayAdapter(), 
            new PricingCalculator())
        {
        }

        // 2. NOWY KONSTRUKTOR: Pozwala na wstrzykiwanie zależności (Dependency Injection)
        // Idealny, gdybyś chciał w przyszłości pisać testy jednostkowe.
        public SubscriptionRenewalService(
            ICustomerRepository customerRepository,
            ISubscriptionPlanRepository planRepository,
            IBillingGateway billingGateway,
            IPricingCalculator pricingCalculator)
        {
            _customerRepository = customerRepository;
            _planRepository = planRepository;
            _billingGateway = billingGateway;
            _pricingCalculator = pricingCalculator;
        }

        // 3. GŁÓWNA METODA (Teraz jest krótka i czytelna)
        public RenewalInvoice CreateRenewalInvoice(
            int customerId,
            string planCode,
            int seatCount,
            string paymentMethod,
            bool includePremiumSupport,
            bool useLoyaltyPoints)
        {
            // Walidacja danych wejściowych
            ValidateInputs(customerId, planCode, seatCount, paymentMethod);

            // Pobranie danych przez interfejsy (nie przez 'new')
            var customer = _customerRepository.GetById(customerId);
            var plan = _planRepository.GetByCode(planCode.Trim().ToUpperInvariant());

            if (!customer.IsActive)
            {
                throw new InvalidOperationException("Inactive customers cannot renew subscriptions");
            }

            // OBLICZENIA - Cała ta 100-linijkowa matematyka skrócona do jednej linijki!
            var price = _pricingCalculator.Calculate(customer, plan, seatCount, paymentMethod, includePremiumSupport, useLoyaltyPoints);

            // Zbudowanie obiektu faktury
            var invoice = new RenewalInvoice
            {
                InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{customerId}-{plan.Code.Trim().ToUpperInvariant()}",
                CustomerName = customer.FullName,
                PlanCode = plan.Code.Trim().ToUpperInvariant(),
                PaymentMethod = paymentMethod.Trim().ToUpperInvariant(),
                SeatCount = seatCount,
                BaseAmount = Math.Round(price.BaseAmount, 2, MidpointRounding.AwayFromZero),
                DiscountAmount = Math.Round(price.DiscountAmount, 2, MidpointRounding.AwayFromZero),
                SupportFee = Math.Round(price.SupportFee, 2, MidpointRounding.AwayFromZero),
                PaymentFee = Math.Round(price.PaymentFee, 2, MidpointRounding.AwayFromZero),
                TaxAmount = Math.Round(price.TaxAmount, 2, MidpointRounding.AwayFromZero),
                FinalAmount = Math.Round(price.FinalAmount, 2, MidpointRounding.AwayFromZero),
                Notes = price.Notes.Trim(),
                GeneratedAt = DateTime.UtcNow
            };

            // ZAPIS I WYSYŁKA - Korzystamy z naszej "wtyczki" (adaptera), a nie zewnętrznej klasy!
            _billingGateway.SaveInvoice(invoice);

            if (!string.IsNullOrWhiteSpace(customer.Email))
            {
                string subject = "Subscription renewal invoice";
                string body = $"Hello {customer.FullName}, your renewal for plan {plan.Code.Trim().ToUpperInvariant()} has been prepared. Final amount: {invoice.FinalAmount:F2}.";
                
                _billingGateway.SendEmail(customer.Email, subject, body);
            }

            return invoice;
        }

        // Metoda pomocnicza, żeby nie śmiecić if-ami rzucającymi wyjątki na samej górze
        private void ValidateInputs(int customerId, string planCode, int seatCount, string paymentMethod)
        {
            if (customerId <= 0) throw new ArgumentException("Customer id must be positive");
            if (string.IsNullOrWhiteSpace(planCode)) throw new ArgumentException("Plan code is required");
            if (seatCount <= 0) throw new ArgumentException("Seat count must be positive");
            if (string.IsNullOrWhiteSpace(paymentMethod)) throw new ArgumentException("Payment method is required");
        }
    }
}