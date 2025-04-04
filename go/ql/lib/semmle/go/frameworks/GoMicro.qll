/**
 * Provides models of the [Go Micro library](https://github.com/go-micro/go-micro).
 */

import go
private import semmle.go.security.RequestForgeryCustomizations

/**
 * Module for Go Micro framework.
 */
module GoMicro {
  /**
   * A GoMicro server type.
   */
  class GoMicroServerType extends Type {
    GoMicroServerType() { this.hasQualifiedName("go-micro.dev/v4/server", "Server") }
  }

  /**
   * A GoMicro client type.
   */
  class GoMicroClientType extends Type {
    GoMicroClientType() { this.hasQualifiedName("go-micro.dev/v4/client", "Client") }
  }

  /**
   * A file that is generated by the protobuf compiler.
   */
  class ProtocGeneratedFile extends File {
    ProtocGeneratedFile() { this.getBaseName().regexpMatch(".*\\.pb(\\.micro)?\\.go$") }
  }

  /**
   * A type that is generated by the protobuf compiler.
   */
  class ProtocMessageType extends Type {
    ProtocMessageType() {
      this.getLocation().getFile() instanceof ProtocGeneratedFile and
      exists(MethodDecl md |
        md.getName() = "ProtoMessage" and
        this = md.getReceiverDecl().getTypeExpr().getAChild().(TypeName).getType()
      )
    }
  }

  /**
   * A Server Interface type.
   */
  class ServiceInterfaceType extends InterfaceType {
    DefinedType definedType;

    ServiceInterfaceType() {
      this = definedType.getUnderlyingType() and
      definedType.getLocation().getFile() instanceof ProtocGeneratedFile
    }

    /**
     * Gets the name of the interface.
     */
    override string getName() { result = definedType.getName() }

    /** DEPRECATED: Use `getDefinedType` instead. */
    deprecated DefinedType getNamedType() { result = definedType }

    /**
     * Gets the defined type on top of this interface type.
     */
    DefinedType getDefinedType() { result = definedType }
  }

  /**
   * A Service server handler type.
   */
  class ServiceServerType extends DefinedType {
    ServiceServerType() {
      this.implements(any(ServiceInterfaceType i)) and
      this.getName().regexpMatch("(?i).*Handler") and
      this.getLocation().getFile() instanceof ProtocGeneratedFile
    }
  }

  /**
   * A Client server handler type.
   */
  class ClientServiceType extends DefinedType {
    ClientServiceType() {
      this.implements(any(ServiceInterfaceType i)) and
      this.getName().regexpMatch("(?i).*Service") and
      this.getLocation().getFile() instanceof ProtocGeneratedFile
    }
  }

  /**
   * A service register handler.
   */
  class ServiceRegisterHandler extends Function {
    ServiceRegisterHandler() {
      this.getName().regexpMatch("(?i)register" + any(ServiceServerType c).getName()) and
      this.getParameterType(0) instanceof GoMicroServerType and
      this.getLocation().getFile() instanceof ProtocGeneratedFile
    }
  }

  bindingset[m]
  pragma[inline_late]
  private predicate implementsServiceType(Method m) {
    m.implements(any(ServiceInterfaceType i).getDefinedType().getMethod(_))
  }

  /**
   * A service handler.
   */
  class ServiceHandler extends Method {
    ServiceHandler() {
      exists(DataFlow::CallNode call |
        call.getTarget() instanceof ServiceRegisterHandler and
        this = call.getArgument(1).getType().getMethod(_) and
        implementsServiceType(this)
      )
    }
  }

  /**
   * A client service function.
   */
  class ClientService extends Function {
    ClientService() {
      this.getName().regexpMatch("(?i)new" + any(ClientServiceType c).getName()) and
      this.getParameterType(0) instanceof StringType and
      this.getParameterType(1) instanceof GoMicroClientType and
      this.getLocation().getFile() instanceof ProtocGeneratedFile
    }
  }

  /**
   * An SSRF sink for the Client service function.
   */
  class ClientRequestUrlAsSink extends RequestForgery::Sink {
    ClientRequestUrlAsSink() {
      exists(DataFlow::CallNode call |
        call.getArgument(0) = this and
        call.getTarget() instanceof ClientService
      )
    }

    override DataFlow::Node getARequest() { result = this }

    override string getKind() { result = "URL" }
  }

  /**
   * A set of remote requests from a service handler.
   */
  class Request extends RemoteFlowSource::Range instanceof DataFlow::ParameterNode {
    Request() {
      exists(ServiceHandler handler |
        this.asParameter().isParameterOf(handler.getFuncDecl(), 1) and
        handler.getParameterType(0).hasQualifiedName("context", "Context") and
        this.getType().(PointerType).getBaseType() instanceof ProtocMessageType
      )
    }
  }
}
