# Job loss and the Town Square food cart

Create a `StorylineSimulation` with the live roster, town registry, game clock, an
explicit workplace → employer NPC id map, and a seed. Advance time through this host
so NPC actions and observations finish before `DayStarted` performs daily work.
Dispose the host to unsubscribe. Do not separately advance the same clock as well.
For another simulation host, `JobLossStoryline.Observe` accepts actual simulated
hours and `NeedsDecayMultiplier` plugs into `NpcSimulator`'s optional callback.
Dormant NPCs contribute no fabricated wages/performance; this is not a replay/save
system. Observe at most 24 hours per NPC between resets.

The current Job model has no employer id. The supplied map is required and checked;
there is no employee-trait fallback. Verification uses explicit scenario ownership:
Greta manages the General Store, Lenny the Auto Shop, Iris the Diner, and Petra the
Police Station. These are test configurations, not new claims about the roster.
A production host must supply its intended ownership map.

Performance changes once per observed day: `0.08 × (Diligence − 0.5) − 0.06 × neglected
need fraction`, scaled for partial days. A need below 30 counts as neglected. Five
FULL consecutive observed days below 0.30 enable dismissal; a partial/unobserved or
recovered day resets the streak. Daily chance is `0.12 × review day × (1 + 0.75 ×
(1 − employer.Warmth) + 0.25 × (1 − employer.Honesty))`, capped at one. Lower honesty
means less procedural patience; lower warmth means less leniency. All are explicit
MVP tuning choices, not inferred from the design document.

Income is interpreted as hourly wages, accrued from observed work and paid at daily
reset. Wages earned before dismissal are paid; no new wages accrue while unemployed.
Fired NPCs get 1.20× needs decay until employed again. After three days, ambition ≥0.70
and ≥200 savings allow a single food-cart attempt, costing 200. No new living-expense
or currency-conservation system is introduced here.

The cart is a lightweight workplace at the existing Town Square location. Owner and
hire receive real Jobs and a 10–16 WorkShift carved from existing schedules, preserving
outside intervals. Cart owner earnings are 8/hour; assistant earnings are 6/hour.
Daily health starts at 0.50 and changes by `0.08 × (traffic − 0.5) + 0.12 × (Diligence −
0.5) + uniform(−0.06, 0.06)`. Traffic counts distinct observed visitors during 10–16,
excluding cart owners/workers, divided by 10 and capped at one. Health ≤0.20 closes;
health ≥0.80 hires the available adult with highest diligence (id breaks ties).
Without a candidate it waits. Hiring/closure ends this template's arc; subsequent
business expansion, repeat attempts, and employee replacement are out of scope.
Closure reduces BOTH directions of the highest-affinity Family/Romantic tie by 0.15,
clamped to −1. This bilateral stress choice is intentional.

Each beat writes independent MemoryFact instances to participants, actual co-located
witnesses and close ties. InvolvedNpcIds identifies participants, not every recipient.
Actual Active-tier Socialize decisions can share one derived fact per interaction
with a co-located, familiar, positive relationship. Zero LLM calls: no distortion is
implemented, per the task's explicit constraint. Jobs, schedules, cart state and NPC
memory streams supply diegetic discovery data; Unity dialogue/scene rendering belongs
to the Editor integration and is unverified here.

Run `dotnet run --project tools/Verification` from this branch's repository root.
The suite prints complete daily arcs for fixed seeds 0 and 6, memory identities,
negative controls, and all existing regression checks.
