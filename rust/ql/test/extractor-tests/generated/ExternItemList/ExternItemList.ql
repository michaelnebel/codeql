// generated by codegen, do not edit
import codeql.rust.elements
import TestUtils

from ExternItemList x, int getNumberOfAttrs, int getNumberOfExternItems
where
  toBeTested(x) and
  not x.isUnknown() and
  getNumberOfAttrs = x.getNumberOfAttrs() and
  getNumberOfExternItems = x.getNumberOfExternItems()
select x, "getNumberOfAttrs:", getNumberOfAttrs, "getNumberOfExternItems:", getNumberOfExternItems
