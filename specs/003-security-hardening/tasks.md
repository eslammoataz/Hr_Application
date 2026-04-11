# Tasks: Phase 2 - Hierarchy Remediation

## Phase 1: Service Abstraction
- [x] T001 Update `IHierarchyService` with Zig-Zag discovery methods
- [x] T002 Implement batch metadata fetching in `HierarchyService`
- [x] T003 Move Zig-Zag unboxing logic from Query to `HierarchyService`

## Phase 2: Query Refactoring
- [x] T004 Refactor `GetCompanyHierarchyQueryHandler` to use updated service
- [x] T005 Remove legacy mapping methods and redundant recursive calls
- [x] T006 Implement batch child-check optimization to resolve N+1 issues

## Phase 3: Performance & Verification
- [x] T007 Optimize organizational lookups with bulk queries
- [x] T008 Verify Zig-Zag pattern correctness (No Double Discovery)
- [x] T009 Regression test on employee path lookups
- [x] T010 Final SQL profiling (ensure minimal DB roundtrips)
