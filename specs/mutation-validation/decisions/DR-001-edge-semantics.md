# DR-001: Mutation and public cache edge semantics

- Status: Adopted for implementation under the owner's 2026-09-15 request.
- Date: 2026-09-15
- Owners: mutation-validation and public-presentation.

Empty and unknown-only merge patches return the current representation with 200,
after resource lookup and If-Match validation. They do not save, increment a
version, change timestamps, replace children, or invalidate public caches.
Unknown fields in mixed patches are ignored. Recognized fields retain existing
mutation behavior, even when the supplied value equals the stored value.

All stored URLs must contain at most 2048 UTF-16 characters. Over-limit input is
a keyed 400 validation error before persistence. Highlights and social labels
must be unique after trimming with ordinal, case-insensitive comparison.

Only PostgreSQL unique violations on ix_profiles_singleton_key receive the
profile-specific 409 response. Other database failures use central translation
or generic 500 Problem Details without database details.

Pagination accepts offsets through Int32.MaxValue, using 64-bit validation of
(page - 1) * pageSize before the existing integer Skip operation. An unsafe
offset is a keyed X-Page 400; safe beyond-last pages return empty arrays.

If-None-Match uses weak comparison for GET, including wildcard and validator
lists. Invalid validator syntax is ignored and returns 200. A matching validator
returns a bodyless 304 with ETag and Cache-Control retained.

updatedAt remains the maximum PublicUpdatedAt of currently visible records.
It can decrease after deletion or unfeaturing. No monotonic data model or
migration is introduced. ETag hashes the full current representation.

These choices supersede the historical characterization rows identified by
PMV-001 through PMV-006 and PPC-001/PPC-002; historical evidence remains dated.
