# TrustRent Backend AGENTS

Scope: `TrustRent_Backend/**`.
This file wins for backend work.

Read first:
- `TrustRent.Api/Program.cs`
- touched module under `TrustRent.Modules.*`
- matching tests under `TrustRent.Tests/**`

Stack:
- ASP.NET Core 8 Minimal API
- PostgreSQL + EF Core 8
- YARP gateway, SignalR, Hangfire
- Modules: Identity, Catalog, Leasing, Communications, Admin, Shared

Hard rules:
- No controllers. No MVC. No FluentValidation.
- No MediatR. No CQRS. No event bus.
- Endpoints are static extension methods on `IEndpointRouteBuilder`.
- Each module owns its `DbContext` and migrations.
- Cross-module work uses `TrustRent.Shared` contracts or direct DI.
- DI wiring lives in `TrustRent.Api/Program.cs`.
- Use full async/await.

Platform actors:
- Guest: browse, view property, open auth.
- Tenant: KYC, apply, visit, confirm interest, pay, open tickets, review.
- Landlord: create/manage property, approve candidate, run lease, handle tickets, review.
- Co-tenant: invited into one application, signs and joins lease if accepted.
- Guarantor: invited after request, submits KYC/income, signs if approved.
- Admin: separate backoffice auth and ops.

Identity and trust:
- Public auth is passwordless email OTP.
- User auth cookie: `trustrent_auth`.
- Admin auth is separate. Admin cookie: `trustrent_admin_auth`.
- KYC uses document extraction: citizen card, no-debt certificate, address proof, tax registration docs.
- Phone onboarding starts in verification endpoints, not profile save.
- Verified phone is immutable; active phone only changes after successful verification.
- User direct email/phone is exposed only when viewer shares a non-rejected application or non-cancelled lease.
- Trust score comes from published reviews, not draft or pending ones.

Marketplace rules:
- One backend serves frontend, backoffice, and mobile.
- Landlord property holds legal and money terms: rent, deposit, advance rent, expense split, regime, accepted periodicities, renewal flag, legal docs.
- Property create must persist before upload jobs.
- Reference data is API-owned and admin-editable. Do not hardcode business lists when backend already owns them.
- Property list/detail may include per-user application flags when auth cookie is present.

Application flow:
- Principal tenant owns the application.
- Base path: `Pending -> VisitCounterProposed/VisitAccepted -> InterestConfirmed -> optional IncomeValidationRequested -> Accepted -> LeaseStartDateProposed/LeaseStartDateConfirmed -> GeneratingContract/ContractPendingSignature -> AwaitingPayment -> LeaseActive`.
- `Rejected` is terminal.
- Landlord approval auto-rejects other active applications for the same property.
- If property has accepted periodicities, requested duration must match one. If none exist, manual duration is valid.
- Co-tenant invite can start by email. Do not require existing user before invite.
- Co-tenant and guarantor side effects happen after save and stay best effort.
- Income validation is landlord-requested, tenant/co-tenant-uploaded, and needs 3 distinct recent payslips with exact NIF match.

Guarantor flow:
- Guarantor is decided in application flow, never in property form.
- Requirement path: `Requested -> Submitted -> LandlordReviewing -> Approved/Rejected/Waived`.
- Application may sit in `GuarantorRequested`, `GuarantorReview`, or `GuarantorRejected` while this is unresolved.
- Approved guarantor becomes a lease signatory when required.

Lease flow:
- Lease begins with start-date negotiation, then contract generation, then signatures, then payment, then active lease.
- Signature order is `Landlord -> Tenant -> CoTenant -> Guarantor`.
- Application status mirrors contract/payment progress.
- Only `AwaitingPayment` leases should activate after payment.
- If lease already became `Active`, stuck application state must reconcile to `LeaseActive`.
- Tax registration is a lease flow with document extraction, not a free text toggle.

Payments and legal flows:
- Rent payments use Stripe PaymentIntents.
- Payment method setup uses SetupIntent and supports `card` and `revolut_pay`.
- Landlord payouts use Stripe Connect.
- Split payment between co-tenants is supported.
- Renewal, early termination, and rent increase are legal flows with deadlines, logs, and notifications; they are not ad-hoc status edits.
- Legal communications must keep audit value: timestamp, IP, viewed/ack state, stable content.

Tickets, reviews, communications:
- Maintenance tickets belong to an active lease/property relationship.
- Tenant opens ticket. Landlord handles normal progress. Tenant can mark resolved; landlord handles the other transitions.
- Review flow is double-blind: `Pending -> Submitted -> Published` when both submit.
- If review window expires, submitted side may publish and untouched side expires.
- Only published reviews affect trust score.
- Notifications, chat, and email are best effort after durable state changes.
- Channel preferences stay at API orchestration layer, not buried inside shared senders.

Admin and ops:
- Admin auth uses separate JWT scheme, CSRF, RBAC, audit, and MFA expectations.
- Admin manages moderation, reference data, settings, communications, jobs, support tickets, reports, and staging access.
- Reports may be anonymous and carry technical metadata.
- Admin detail endpoints must project flat DTOs. Do not return raw EF graphs.
- Simulate endpoints stay dev/staging only. Never leak prod access.

Tests:
- xUnit + FluentAssertions + Moq + EF InMemory.
- Keep `TrustRent.Tests/TestAssemblyBootstrap.cs`. Encryption init matters.
- If running API locks DLL copy, validate with `dotnet msbuild .\TrustRent.Api\TrustRent.Api.csproj /t:Compile`.
- Run narrow tests first, then wider `dotnet test`.

Need more detail:
- read `README.md`