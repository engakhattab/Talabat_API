# Requirements Quality Checklist: Unified User Identity and Persistence Cutover

**Purpose**: Review whether the Phase 2 requirements are complete, clear, consistent, measurable,
and ready for implementation planning  
**Created**: 2026-07-18  
**Feature**: [spec.md](../spec.md)  
**Depth**: Standard  
**Audience**: Pull-request and implementation-plan reviewers

## Requirement Completeness

- [ ] CHK001 Are the required inputs, assigned account state, capability/role effects, and failure
  outcomes documented for both registration flows? [Completeness, Spec §FR-003–FR-004, §FR-009]
- [ ] CHK002 Are outcomes defined when customer grant, approval, rejection, or deactivation targets a
  user identifier that does not exist? [Gap, Exception Flow]
- [ ] CHK003 Are the authorized actor, accepted input, valid source states, and prohibited
  self-service paths documented for both agent decisions? [Completeness, Spec §FR-005–FR-006]
- [ ] CHK004 Are rollback requirements documented for every mutation stage—account creation,
  profile/application state, capability state, authorization role, and session-validity state?
  [Completeness, Spec §FR-011–FR-012]
- [ ] CHK005 Are requirements defined for repeated deactivation and deactivation of an already
  deleted or missing user? [Gap, Edge Case]
- [ ] CHK006 Are role-seeding failure, retry, partial-existing-role, and repeated-startup outcomes
  specified in addition to the final role count? [Completeness, Spec §FR-021, §SC-008]
- [ ] CHK007 Is the complete capability-to-role projection documented, including preservation of
  unrelated capabilities and roles for multi-capability users? [Completeness, Spec §FR-007,
  §FR-012, §FR-021]
- [ ] CHK008 Are the replacement registration operations and retained login, logout, and current-user
  response contracts specified sufficiently to identify every externally observable contract
  change? [Completeness, Spec §FR-022–FR-023]

## Requirement Clarity

- [ ] CHK009 Is “already-used normalized email/sign-in name” defined consistently with the retained
  normalization and uniqueness policy, including case and surrounding whitespace? [Clarity,
  Spec §FR-003–FR-004, §FR-009, Assumptions]
- [ ] CHK010 Is “one unified user record per person” precise about the uniqueness key and how a
  returning delivery agent becomes a customer without a duplicate account? [Clarity, Spec §FR-001,
  §FR-007, §SC-001]
- [ ] CHK011 Is “admin-controlled service-level decision” defined clearly enough to distinguish the
  authorized initiator from transport endpoints, applicants, and future admin UI? [Ambiguity,
  Spec §FR-005–FR-006, Out of Scope]
- [ ] CHK012 Is the Phase 2 boundary explicit that deactivation atomically refreshes session-validity
  state and configures an exact five-minute validator, while elapsed-time rejection of an already
  issued cookie remains Phase 3? [Clarity, Spec §FR-016–FR-017, §SC-004]
- [ ] CHK013 Is “current persisted Customer capability state” specific about which protected
  customer flows use it and how stale role claims are treated? [Clarity, Spec §FR-018, §FR-025]
- [ ] CHK014 Is “ordinary user loads” defined sufficiently to distinguish business/account lookup,
  audit/history access, and explicitly prohibited deleted-user bypasses? [Ambiguity, Spec §FR-028]
- [ ] CHK015 Is the scope of “byte-for-byte unchanged” tied to an identified baseline set of status,
  payload, error-code, and content-type assertions? [Measurability, Spec §FR-023, §SC-005]
- [ ] CHK016 Are all eight unified-user integrity rules individually identifiable from the
  requirements rather than referenced only as a count? [Clarity, Spec §FR-029–FR-033, §FR-041]

## Requirement Consistency

- [ ] CHK017 Do the one-account requirements remain consistent with the explicitly destructive,
  non-data-preserving development rebuild assumption? [Consistency, Spec §FR-001, §FR-042,
  Assumptions]
- [ ] CHK018 Does the atomic rollback guarantee remain consistent with the assumption that full
  role-failure injection is deferred to Phase 3, without weakening Phase 2 acceptance? [Potential
  Conflict, Spec §FR-011, §SC-003, Assumptions]
- [ ] CHK019 Are capability-source-of-truth, synchronized-role, and stale-token requirements
  consistent about which representation controls business access versus authorization projection?
  [Consistency, Spec §FR-012, §FR-018]
- [ ] CHK020 Are deactivation requirements consistent with available-agent selection, particularly
  whether an inactive user whose operational state remains Available may be returned? [Ambiguity,
  Spec §FR-016, §FR-026]
- [ ] CHK021 Are soft-delete lookup exclusion and explicit login rejection requirements consistent,
  including the expected result when the account lookup cannot see the deleted user? [Consistency,
  Spec §FR-015, §FR-028]
- [ ] CHK022 Are independent address-write concurrency requirements consistent with the unique
  active-default-address invariant when two writers select defaults concurrently? [Potential
  Conflict, Spec §FR-033, §FR-035, §FR-037]
- [ ] CHK023 Is dependency-only vulnerability remediation explicitly compatible with the requirement
  that the Delivery API remain an otherwise unchanged compiling scaffold? [Ambiguity, Spec §FR-045,
  §SC-012, Out of Scope]

## Acceptance Criteria Quality

- [ ] CHK024 Can the “exactly one user identifier per person” outcome be objectively measured for
  new registration, normalized duplicate email, and existing-account onboarding? [Measurability,
  Spec §SC-001]
- [ ] CHK025 Does the 100% rollback criterion enumerate the failure classes and workflow operations
  included in its denominator? [Clarity, Spec §SC-003, §FR-011]
- [ ] CHK026 Does the compatibility criterion identify the complete baseline artifact or assertion
  set used to establish byte-for-byte equivalence? [Traceability, Spec §SC-005, §FR-023]
- [ ] CHK027 Does the post-rebuild criterion define the expected identity of each role, integrity
  rule, relationship, table exclusion, and schema-history entry—not only aggregate counts?
  [Completeness, Spec §SC-007, §FR-040–FR-042]
- [ ] CHK028 Is full-suite acceptance measurable when the number of migrated versus new acceptance
  checks can change during implementation? [Measurability, Spec §SC-009]

## Scenario Coverage

- [ ] CHK029 Are rejection followed by resubmission and eventual approval requirements preserved
  across the persistence cutover? [Coverage, Alternate Flow, Spec §User Story 2, Gap]
- [ ] CHK030 Is the same-account DeliveryAgent-to-Customer journey covered with explicit preservation
  of application, operational, role, and historical state? [Coverage, Alternate Flow,
  Spec §User Story 1 Scenario 3, §FR-007]
- [ ] CHK031 Are missing-target-user scenarios covered consistently across all existing-account
  workflows? [Coverage, Exception Flow, Gap]
- [ ] CHK032 Are requirements for recovery from role synchronization or session-state update failure
  complete enough to prove no partial account/capability state remains? [Coverage, Recovery Flow,
  Spec §FR-011, §SC-003]
- [ ] CHK033 Are requirements defined for failure after a destructive rebuild has started, including
  the recoverable checkpoint and expected database state before retry? [Coverage, Recovery Flow,
  Spec §FR-043–FR-044, Gap]

## Edge Case Coverage

- [ ] CHK034 Are simultaneous approval, customer onboarding, deactivation, and business-write
  conflicts addressed with a deterministic accepted/rejected outcome? [Coverage, Concurrency,
  Spec §FR-035–FR-037]
- [ ] CHK035 Are missing, renamed, duplicated, or partially seeded authorization roles addressed in
  the requirements? [Coverage, Edge Case, Spec §FR-011, §FR-021]
- [ ] CHK036 Are malformed subject identifiers, stale cookie roles, stale token roles, inactive
  accounts, and deleted accounts each assigned a distinct and unambiguous outcome? [Coverage,
  Security Edge Cases, Spec §FR-015–FR-019]

## Non-Functional and Security Requirements

- [ ] CHK037 Is the authoritative source for retained credential, password, normalization, lockout,
  rate-limiting, privacy, and compliance requirements identified rather than only described as an
  unchanged baseline? [Dependency, Spec §Assumptions]
- [ ] CHK038 Are observability requirements defined for account-workflow rollback, role-seeding
  failure, login rejection, concurrency conflicts, and destructive-rebuild aborts? [Gap,
  Operational Readiness]
- [ ] CHK039 Is the zero-known-vulnerability gate scoped to all projects and transitive dependencies,
  with an unambiguous rule for blocking acceptance? [Clarity, Spec §SC-012]

## Dependencies and Assumptions

- [ ] CHK040 Are Phase 1 acceptance/commit, the exact authorized development connection, disposable
  data approval, clean-checkpoint state, green build/tests, and Phase 3 deferrals all documented as
  independently verifiable dependencies without contradictory handoff criteria? [Completeness,
  Spec §FR-043–FR-044, Assumptions]
