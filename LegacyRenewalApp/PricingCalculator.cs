using System;

namespace LegacyRenewalApp
{
    // 1. Klasa pomocnicza, która "zbiera" wszystkie wyniki w jedną paczkę
    public class PricingResult
    {
        public decimal BaseAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal SupportFee { get; set; }
        public decimal PaymentFee { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    // 2. Interfejs naszego kalkulatora
    public interface IPricingCalculator
    {
        PricingResult Calculate(Customer customer, SubscriptionPlan plan, int seatCount, string paymentMethod, bool includePremiumSupport, bool useLoyaltyPoints);
    }

    // 3. Właściwa logika obliczeń (przeklejona ze starego serwisu)
    public class PricingCalculator : IPricingCalculator
    {
        public PricingResult Calculate(Customer customer, SubscriptionPlan plan, int seatCount, string paymentMethod, bool includePremiumSupport, bool useLoyaltyPoints)
        {
            var result = new PricingResult();
            string normalizedPlanCode = plan.Code.Trim().ToUpperInvariant();
            string normalizedPaymentMethod = paymentMethod.Trim().ToUpperInvariant();

            // --- BAZA ---
            result.BaseAmount = (plan.MonthlyPricePerSeat * seatCount * 12m) + plan.SetupFee;

            // --- RABATY ---
            if (customer.Segment == "Silver") { result.DiscountAmount += result.BaseAmount * 0.05m; result.Notes += "silver discount; "; }
            else if (customer.Segment == "Gold") { result.DiscountAmount += result.BaseAmount * 0.10m; result.Notes += "gold discount; "; }
            else if (customer.Segment == "Platinum") { result.DiscountAmount += result.BaseAmount * 0.15m; result.Notes += "platinum discount; "; }
            else if (customer.Segment == "Education" && plan.IsEducationEligible) { result.DiscountAmount += result.BaseAmount * 0.20m; result.Notes += "education discount; "; }

            if (customer.YearsWithCompany >= 5) { result.DiscountAmount += result.BaseAmount * 0.07m; result.Notes += "long-term loyalty discount; "; }
            else if (customer.YearsWithCompany >= 2) { result.DiscountAmount += result.BaseAmount * 0.03m; result.Notes += "basic loyalty discount; "; }

            if (seatCount >= 50) { result.DiscountAmount += result.BaseAmount * 0.12m; result.Notes += "large team discount; "; }
            else if (seatCount >= 20) { result.DiscountAmount += result.BaseAmount * 0.08m; result.Notes += "medium team discount; "; }
            else if (seatCount >= 10) { result.DiscountAmount += result.BaseAmount * 0.04m; result.Notes += "small team discount; "; }

            if (useLoyaltyPoints && customer.LoyaltyPoints > 0)
            {
                int pointsToUse = customer.LoyaltyPoints > 200 ? 200 : customer.LoyaltyPoints;
                result.DiscountAmount += pointsToUse;
                result.Notes += $"loyalty points used: {pointsToUse}; ";
            }

            decimal subtotalAfterDiscount = result.BaseAmount - result.DiscountAmount;
            if (subtotalAfterDiscount < 300m)
            {
                subtotalAfterDiscount = 300m;
                result.Notes += "minimum discounted subtotal applied; ";
            }

            // --- WSPARCIE ---
            if (includePremiumSupport)
            {
                if (normalizedPlanCode == "START") result.SupportFee = 250m;
                else if (normalizedPlanCode == "PRO") result.SupportFee = 400m;
                else if (normalizedPlanCode == "ENTERPRISE") result.SupportFee = 700m;
                result.Notes += "premium support included; ";
            }

            // --- OPŁATA ZA PŁATNOŚĆ ---
            if (normalizedPaymentMethod == "CARD") { result.PaymentFee = (subtotalAfterDiscount + result.SupportFee) * 0.02m; result.Notes += "card payment fee; "; }
            else if (normalizedPaymentMethod == "BANK_TRANSFER") { result.PaymentFee = (subtotalAfterDiscount + result.SupportFee) * 0.01m; result.Notes += "bank transfer fee; "; }
            else if (normalizedPaymentMethod == "PAYPAL") { result.PaymentFee = (subtotalAfterDiscount + result.SupportFee) * 0.035m; result.Notes += "paypal fee; "; }
            else if (normalizedPaymentMethod == "INVOICE") { result.PaymentFee = 0m; result.Notes += "invoice payment; "; }
            else throw new ArgumentException("Unsupported payment method");

            // --- PODATKI ---
            decimal taxRate = 0.20m;
            if (customer.Country == "Poland") taxRate = 0.23m;
            else if (customer.Country == "Germany") taxRate = 0.19m;
            else if (customer.Country == "Czech Republic") taxRate = 0.21m;
            else if (customer.Country == "Norway") taxRate = 0.25m;

            decimal taxBase = subtotalAfterDiscount + result.SupportFee + result.PaymentFee;
            result.TaxAmount = taxBase * taxRate;
            result.FinalAmount = taxBase + result.TaxAmount;

            if (result.FinalAmount < 500m)
            {
                result.FinalAmount = 500m;
                result.Notes += "minimum invoice amount applied; ";
            }

            return result;
        }
    }
}