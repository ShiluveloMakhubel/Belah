# Indicative fragrance price references

Checked 2026-10-13. These are third-party South African retail observations, not Belah selling prices. Prices may change and may reflect promotions or unavailable products. They remain unapproved in the seed records. Where the supplied entry does not identify a specific scent, no numeric reference is assigned. No bottle size is assumed as Belah inventory; reference sizes below are only the sizes on the cited retailer listing.

| Intake name | Reference only | Source and limitation |
| --- | ---: | --- |
| Oud Stallion Cologne | R5,000 | [Verbe Activ](https://verbe.co.za/products/maison-crivelli-oud-stallion-extrait-de-parfum-50ml) listed Maison Crivelli Oud Stallion Extrait de Parfum 50 ml at R5,000. The supplied “Cologne” wording conflicts with that listing; verify stock identity and size. |
| Parfums de Marly Paris | Pending | The scent is not identified; no price assigned. |
| Amouage | Pending | The scent is not identified; no price assigned. |
| Bvlgari Aqva Pour Homme | R2,295 | [Ascot Cosmetics](https://ascotcosmetics.co.za/product/bvlgari-aqva-pour-homme-eau-de-toilette-spray-100ml/) listed 100 ml EDT at R2,295. Belah size and current price require approval. |
| Bvlgari Omnia | Pending | The exact Omnia fragrance is not identified; no price assigned. |
| Pink Sapphire | Pending | Manufacturer and fragrance match are unresolved; no price assigned. |
| Maison Margiela Paris | Pending | The exact fragrance is not identified; no price assigned. |
| Valentina Eau de Parfum | R1,895 | [PriceCheck](https://www.pricecheck.co.za/categories/186/Perfumes?brand=valentino) showed Valentino Valentina EDP 80 ml from R1,895. Confirm bottle and size. |
| Sì Passione Eau de Parfum — Giorgio Armani | R1,800 | [Woolworths](https://www.woolworths.co.za/cat/Beauty/Armani/Women-s-Fragrance/_/N-0Zybkjf6) showed this EDP from R1,800 with 30, 50, and 100 ml options. The seed price is a starting reference only; no Belah size is assumed. |
| Coach New York Eau de Parfum | R2,260 | [Clicks](https://clicks.co.za/coach_eau-de-parfum-90ml/p/272166) listed Coach Eau de Parfum 90 ml at R2,260 and describes it as inspired by New York. Confirm bottle/size. |

The five numeric prices are stored with `IsPriceApproved = false`; the other five have `Price = 0` and `IsPriceApproved = false`. The storefront labels numeric values as indicative and zero values as pending approval. The API rejects order creation until a price is approved and physical stock is confirmed.
