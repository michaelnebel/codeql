---
category: minorAnalysis
---
The `cs/linq/missed-where` query no longer reports loops that use an `out` variable declared in the filtering condition elsewhere in the loop body, where rewriting the condition as a LINQ predicate would put the variable out of scope.
