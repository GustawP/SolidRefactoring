# Legacy Code Refactoring (SOLID)

Refactoring of a legacy subscription-renewal service in C# (.NET 8), done for the APBD course at PJAIT. The original task is in [docs/TASK.en.md](docs/TASK.en.md) ([Polish version](docs/TASK.pl.md)).

## The problem

`SubscriptionRenewalService.CreateRenewalInvoice` was one long method that validated input, loaded data, calculated discounts, fees and tax, built the invoice, saved it and sent an email. It created its own dependencies with `new` and called a static third-party class, `LegacyBillingGateway`, directly.

The constraints made it a real refactoring exercise rather than a rewrite:

- the client project, `LegacyRenewalAppConsumer`, must not change, so the public contract must stay exactly the same;
- `LegacyBillingGateway` must not change either;
- the calculated invoice must stay the same.

## What changed

| Before | After |
|---|---|
| One method doing everything | `SubscriptionRenewalService` orchestrates; calculation, data access and billing live in their own classes |
| Dependencies created with `new` inside the service | Injected through the constructor as interfaces (`ICustomerRepository`, `ISubscriptionPlanRepository`, `IPricingCalculator`, `IBillingGateway`) |
| Direct calls to the static `LegacyBillingGateway` | Wrapped by `BillingGatewayAdapter` behind `IBillingGateway` (Adapter pattern) |
| Pricing logic mixed with invoice building | Moved to `PricingCalculator`, returning a `PricingResult` |
| Input checks interleaved with logic | Collected in `ValidateInputs` |

**Keeping the contract.** The parameterless constructor the client relies on still exists and chains to the new injecting constructor with the default implementations, so the client compiles and behaves as before while the service itself can now be tested with fakes.

## Running it

Requires the .NET 8 SDK.

```bash
dotnet run --project LegacyRenewalAppConsumer
```

## What I would do next

The refactoring removes coupling, but `PricingCalculator` still holds the discount, support-fee, payment-fee and tax rules as `if`/`else` chains. The next step, which the task suggests, is the Open/Closed Principle: one small class per rule behind a common interface (`IDiscountRule`, `IPaymentFeePolicy`, `ITaxPolicy`), so a new customer segment or payment method is added as a new class rather than by editing the calculator. Unit tests for the calculator, using the injecting constructor, would lock the current results in before that change.
