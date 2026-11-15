# Wave rating implementation notes

Sources: prototype-mvp.docx sections 4.4, 4.6, 4.8; ui-screens.docx S1 and S4.

- Score each request's arrival-to-service waiting time, excluding service and pause.
- Defaults: grace 10s, limit 30s, unit floor 25%, wave floor 40%, Ocean View weight 1.5 (range 1-3).
- Ratings: 40/55/70/80/90%, Perfect only if every request waited within grace. Keep the minimum result reachable as 1 star.
- Show satisfaction only after Wave Complete. During play show queues and warnings.
- ServiceStatusView uses the rating catalog's grace time for the simple long-wait warning. A queued service without a worker takes warning priority. No queue means no warning; warnings are hidden outside active play. This is the agreed simple warning scope, not the full bottleneck state machine.
- Prototype rewards: Coins, Energy, Resort Value. No Reputation, Materials or Gems.
- Coins use the existing per-service rewards and rating multipliers (0.4/0.6/0.8/1/1.2/1.3).
- Energy has no gameplay cap or offline regeneration; it is an optional boost, never a requirement for Perfect.
- Resort Value is a growing status score, never spent and never used for prototype area unlocks.
- Energy by rating (build section 4.6): 0/1/2/3/5 for 1-5 stars; user confirmed Perfect also grants 5. Tune all six values in EconomyCatalog. Empty waves grant zero.
- User-approved temporary Resort Value tuning: +10/20/30/40/50/60 for 1-5 stars/Perfect. Empty waves give zero. Expose the per-tier gain in EconomyCatalog.
- Freeze rewards in WaveResult and claim all once before confirming the next wave; do not show a different amount after tuning changes.
- Progress currently reports from RequestArrivalsBehaviour.LateUpdate. Result creation must also handle an earlier synchronous RequestCompleted progress listener by reading authoritative completed history.
- Rebuild Economy and Results history after re-enabling. UI must use ResultReady and Results.TryConfirmResult, never bypass rewards through WaveController.TryConfirmResult.
- This task covers wave reward gains. Resort Value from building upgrades and the Energy boost/ad flows are separate work.
- No commits without user review and explicit authorization.
