private import ruby
private import codeql.ruby.frameworks.ActionDispatch
private import codeql.ruby.frameworks.ActionController

query predicate actionDispatchRoutes(
  ActionDispatch::Route r, string method, string path, string controller, string action
) {
  r.getHTTPMethod() = method and
  r.getPath() = path and
  r.getController() = controller and
  r.getAction() = action
}

query predicate actionDispatchControllerMethods(
  ActionDispatch::Route r, ActionControllerActionMethod m
) {
  m.getARoute() = r
}
