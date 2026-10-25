# Reusable event tickets

Lumen the Barmaid in Lorencia sells Devil Square Ticket, Blood Castle Ticket
and Kalima Ticket. Initial test stock has **10 uses per ticket** and costs
**1,000 Zen per ticket**, before any castle tax. These are the existing client
ticket types, not bundles of ten normal invitations or maps.

## How to use them

- BC and DS: carry the matching ticket and enter through the normal event NPC.
  Normal event levels, class restrictions, schedules, entrance fees and capacity
  checks still apply. One use is spent only after the event accepts entry.
- Kalima: right-click the ticket outside a safe zone and outside Kalima or an
  event. It creates the normal Kalima portal for the highest stage allowed by
  the character's level. One use is spent when the portal is successfully created.
  Walking through that portal does not spend another use.
- Remaining uses are stored on the item and survive reconnects. The last use
  removes the ticket. Failed entry or portal creation does not spend a use.
- Tickets can be kept in the vault, but cannot be traded, dropped, sold to an
  NPC, offered in a personal store or repaired. This follows the existing client
  item rules and prevents repairs from restoring spent uses.
- Normal BC cloaks, DS invitations and Lost Maps continue to work.

## Changing quantity and price

In the admin configuration editor, open Lumen's merchant store and edit the
ticket listing. **Durability is the number of remaining uses.** Set it to an
integer such as **1**, **5** or **10**. Change **StorePrice** to set the Zen price.
Follow the editor's usual save and configuration-reload workflow.

To offer several quantities together, create separate merchant listings using
the same ticket item definition, each with its own durability, price and free
shop slot. Keep item level at zero. Do not enable repair or stack different
ticket items together. Existing purchased tickets retain their remaining uses;
changing a shop listing affects future purchases.

The startup update adds initial stock once. It does not reset later shop edits
or restore the test listings after an administrator removes them. Ticket use
does not depend on Lumen: a future X Shop can grant the same item definitions
with the desired remaining-use count. X Shop itself is not enabled by this change.

## Kalima level selection

Normal classes use minimum levels 40, 131, 181, 231, 281, 331 and 350 for
Kalima 1 through 7. MG, DL and RF use 20, 111, 161, 211, 261, 311 and 350.
These are the existing Season 6 client thresholds. The reusable ticket picks
the highest eligible stage; it does not bypass the minimum entry level.
