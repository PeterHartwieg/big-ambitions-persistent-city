# Vehicle ownership regression

Run `python3 tools/vehicle-ownership-regression/run.py`. Use `--source-root /path/to/frozen/source` to check a candidate snapshot.

The harness compiles the actual `VehicleScriptOwnership` implementation and extracts the actual `RandomAmbient`, `Ordinary` and body ownership-validation methods. Controlled Unity doubles cover live addition/removal/reparenting, inactive services and subclasses, destroyed/missing components, assembly policy boundaries, nested checks, cleanup after a query exception, bounded buffer retention, stable type-cache size, pool reuse, and changed native body/path/marker identity. The unchanged legacy predicate is an independent oracle for fixture decisions. No game assemblies are needed.

Run the sibling `native-command-regression` harness for deferred physics, including ownership loss inside the final permission callback. These offline checks do not establish native Unity query parity, allocation behavior or frame-time improvement; those require isolated game testing.
