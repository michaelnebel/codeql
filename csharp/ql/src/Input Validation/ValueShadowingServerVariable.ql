/**
 * @name Value shadowing: server variable
 * @description Finds ambiguous accesses to server variables
 * @kind problem
 * @problem.severity warning
 * @precision medium
 * @id cs/web/ambiguous-server-variable
 * @tags security
 *       maintainability
 *       frameworks/asp.net
 *       external/cwe/cwe-348
 */

import csharp
import semmle.code.csharp.frameworks.system.web.Http

from IndexerAccess ia
where
  ia.getTarget().getDeclaringType().hasFullyQualifiedName("System.Web", "HttpRequest") and
  isServerVariable(ia.getIndex(0))
select ia, "Ambiguous access to server variable."
