private import codeql.swift.generated.decl.PatternBindingDecl

module Impl {
  class PatternBindingDecl extends Generated::PatternBindingDecl {
    override string toStringImpl() { result = "var ... = ..." }
  }
}
