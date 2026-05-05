# Negotiation System Test Matrix

Date: 2026-05-04

This checklist maps the production negotiation redesign phases to concrete verification scenarios.

## Automated Build

- [x] `dotnet build KamPay\KamPay.csproj -f net10.0-windows10.0.19041.0 --no-restore -v:minimal /clp:ErrorsOnly`

## Core Scenarios

- [x] Buyer creates sale offer: creates `negotiation_offers/{transactionId}/{offerId}` and sets `CurrentActiveOfferId`.
- [x] Seller creates counter offer: previous active offer becomes `Superseded`.
- [x] Buyer creates next counter offer: previous active offer becomes `Superseded`.
- [x] User cannot accept or reject their own active offer.
- [x] Accepting active offer sets offer `Accepted` and transaction `IsNegotiating=false`.
- [x] Rejecting active offer sets offer `Rejected` and transaction `Rejected`.

## Edge Cases

- [x] Stale UI: inactive chat offer messages cannot be accepted.
- [x] Race guard: transaction is re-read before offer write and rejected if `LastActionBy`, `NegotiationRoundCount`, or `CurrentActiveOfferId` changed.
- [x] Expiry: expired negotiations set active offer `Expired` and transaction `Expired`.
- [x] Product deletion: active negotiations are closed and affected buyers are notified.
- [x] Manual sold flow: active negotiations are closed and affected buyers are notified.
- [x] Product availability: new offers are rejected when product is inactive, sold, or reserved.
- [x] Legacy fallback: old scalar offer fields produce a fallback active offer/history item when no offer collection exists.
