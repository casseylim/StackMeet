# Resolved API target dependency edges

| Package/version | Dependencies |
|---|---|
| Azure.Core/1.35.0 | Microsoft.Bcl.AsyncInterfaces, System.Diagnostics.DiagnosticSource, System.Memory.Data, System.Numerics.Vectors, System.Text.Encodings.Web, System.Text.Json, System.Threading.Tasks.Extensions |
| Azure.Identity/1.10.3 | Azure.Core, Microsoft.Identity.Client, Microsoft.Identity.Client.Extensions.Msal, System.Memory, System.Security.Cryptography.ProtectedData, System.Text.Json, System.Threading.Tasks.Extensions |
| Humanizer.Core/2.14.1 | — |
| Microsoft.Bcl.AsyncInterfaces/6.0.0 | — |
| Microsoft.CodeAnalysis.Analyzers/3.3.3 | — |
| Microsoft.CodeAnalysis.Common/4.5.0 | Microsoft.CodeAnalysis.Analyzers, System.Collections.Immutable, System.Reflection.Metadata, System.Runtime.CompilerServices.Unsafe, System.Text.Encoding.CodePages |
| Microsoft.CodeAnalysis.CSharp/4.5.0 | Microsoft.CodeAnalysis.Common |
| Microsoft.CodeAnalysis.CSharp.Workspaces/4.5.0 | Humanizer.Core, Microsoft.CodeAnalysis.CSharp, Microsoft.CodeAnalysis.Common, Microsoft.CodeAnalysis.Workspaces.Common |
| Microsoft.CodeAnalysis.Workspaces.Common/4.5.0 | Humanizer.Core, Microsoft.Bcl.AsyncInterfaces, Microsoft.CodeAnalysis.Common, System.Composition, System.IO.Pipelines, System.Threading.Channels |
| Microsoft.CSharp/4.5.0 | — |
| Microsoft.Data.SqlClient/5.1.5 | Azure.Identity, Microsoft.Data.SqlClient.SNI.runtime, Microsoft.Identity.Client, Microsoft.IdentityModel.JsonWebTokens, Microsoft.IdentityModel.Protocols.OpenIdConnect, Microsoft.SqlServer.Server, System.Configuration.ConfigurationManager, System.Diagnostics.DiagnosticSource, System.Runtime.Caching, System.Security.Cryptography.Cng, System.Security.Principal.Windows, System.Text.Encoding.CodePages, System.Text.Encodings.Web |
| Microsoft.Data.SqlClient.SNI.runtime/5.1.1 | — |
| Microsoft.EntityFrameworkCore/8.0.8 | Microsoft.EntityFrameworkCore.Abstractions, Microsoft.EntityFrameworkCore.Analyzers, Microsoft.Extensions.Caching.Memory, Microsoft.Extensions.Logging |
| Microsoft.EntityFrameworkCore.Abstractions/8.0.8 | — |
| Microsoft.EntityFrameworkCore.Analyzers/8.0.8 | — |
| Microsoft.EntityFrameworkCore.Design/8.0.8 | Humanizer.Core, Microsoft.CodeAnalysis.CSharp.Workspaces, Microsoft.EntityFrameworkCore.Relational, Microsoft.Extensions.DependencyModel, Mono.TextTemplating |
| Microsoft.EntityFrameworkCore.Relational/8.0.8 | Microsoft.EntityFrameworkCore, Microsoft.Extensions.Configuration.Abstractions |
| Microsoft.EntityFrameworkCore.SqlServer/8.0.8 | Microsoft.Data.SqlClient, Microsoft.EntityFrameworkCore.Relational |
| Microsoft.Extensions.ApiDescription.Server/6.0.5 | — |
| Microsoft.Extensions.Caching.Abstractions/8.0.0 | Microsoft.Extensions.Primitives |
| Microsoft.Extensions.Caching.Memory/8.0.0 | Microsoft.Extensions.Caching.Abstractions, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Logging.Abstractions, Microsoft.Extensions.Options, Microsoft.Extensions.Primitives |
| Microsoft.Extensions.Configuration.Abstractions/8.0.0 | Microsoft.Extensions.Primitives |
| Microsoft.Extensions.DependencyInjection/8.0.0 | Microsoft.Extensions.DependencyInjection.Abstractions |
| Microsoft.Extensions.DependencyInjection.Abstractions/8.0.0 | — |
| Microsoft.Extensions.DependencyModel/8.0.1 | System.Text.Encodings.Web, System.Text.Json |
| Microsoft.Extensions.Logging/8.0.0 | Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging.Abstractions, Microsoft.Extensions.Options |
| Microsoft.Extensions.Logging.Abstractions/8.0.0 | Microsoft.Extensions.DependencyInjection.Abstractions |
| Microsoft.Extensions.Options/8.0.0 | Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Primitives |
| Microsoft.Extensions.Primitives/8.0.0 | — |
| Microsoft.Identity.Client/4.56.0 | Microsoft.IdentityModel.Abstractions |
| Microsoft.Identity.Client.Extensions.Msal/4.56.0 | Microsoft.Identity.Client, System.IO.FileSystem.AccessControl, System.Security.Cryptography.ProtectedData |
| Microsoft.IdentityModel.Abstractions/6.35.0 | — |
| Microsoft.IdentityModel.JsonWebTokens/6.35.0 | Microsoft.IdentityModel.Tokens, System.Text.Encoding, System.Text.Encodings.Web, System.Text.Json |
| Microsoft.IdentityModel.Logging/6.35.0 | Microsoft.IdentityModel.Abstractions |
| Microsoft.IdentityModel.Protocols/6.35.0 | Microsoft.IdentityModel.Logging, Microsoft.IdentityModel.Tokens |
| Microsoft.IdentityModel.Protocols.OpenIdConnect/6.35.0 | Microsoft.IdentityModel.Protocols, System.IdentityModel.Tokens.Jwt |
| Microsoft.IdentityModel.Tokens/6.35.0 | Microsoft.CSharp, Microsoft.IdentityModel.Logging, System.Security.Cryptography.Cng |
| Microsoft.NETCore.Platforms/1.1.0 | — |
| Microsoft.NETCore.Targets/1.1.0 | — |
| Microsoft.OpenApi/1.6.14 | — |
| Microsoft.SqlServer.Server/1.0.0 | — |
| Microsoft.Win32.SystemEvents/6.0.0 | — |
| Mono.TextTemplating/2.2.1 | System.CodeDom |
| Swashbuckle.AspNetCore/6.6.2 | Microsoft.Extensions.ApiDescription.Server, Swashbuckle.AspNetCore.Swagger, Swashbuckle.AspNetCore.SwaggerGen, Swashbuckle.AspNetCore.SwaggerUI |
| Swashbuckle.AspNetCore.Swagger/6.6.2 | Microsoft.OpenApi |
| Swashbuckle.AspNetCore.SwaggerGen/6.6.2 | Swashbuckle.AspNetCore.Swagger |
| Swashbuckle.AspNetCore.SwaggerUI/6.6.2 | — |
| System.CodeDom/4.4.0 | — |
| System.Collections.Immutable/6.0.0 | System.Runtime.CompilerServices.Unsafe |
| System.Composition/6.0.0 | System.Composition.AttributedModel, System.Composition.Convention, System.Composition.Hosting, System.Composition.Runtime, System.Composition.TypedParts |
| System.Composition.AttributedModel/6.0.0 | — |
| System.Composition.Convention/6.0.0 | System.Composition.AttributedModel |
| System.Composition.Hosting/6.0.0 | System.Composition.Runtime |
| System.Composition.Runtime/6.0.0 | — |
| System.Composition.TypedParts/6.0.0 | System.Composition.AttributedModel, System.Composition.Hosting, System.Composition.Runtime |
| System.Configuration.ConfigurationManager/6.0.1 | System.Security.Cryptography.ProtectedData, System.Security.Permissions |
| System.Diagnostics.DiagnosticSource/6.0.1 | System.Runtime.CompilerServices.Unsafe |
| System.Drawing.Common/6.0.0 | Microsoft.Win32.SystemEvents |
| System.Formats.Asn1/5.0.0 | — |
| System.IdentityModel.Tokens.Jwt/6.35.0 | Microsoft.IdentityModel.JsonWebTokens, Microsoft.IdentityModel.Tokens |
| System.IO.FileSystem.AccessControl/5.0.0 | System.Security.AccessControl, System.Security.Principal.Windows |
| System.IO.Pipelines/6.0.3 | — |
| System.Memory/4.5.4 | — |
| System.Memory.Data/1.0.2 | System.Text.Encodings.Web, System.Text.Json |
| System.Numerics.Vectors/4.5.0 | — |
| System.Reflection.Metadata/6.0.1 | System.Collections.Immutable |
| System.Runtime/4.3.0 | Microsoft.NETCore.Platforms, Microsoft.NETCore.Targets |
| System.Runtime.Caching/6.0.0 | System.Configuration.ConfigurationManager |
| System.Runtime.CompilerServices.Unsafe/6.0.0 | — |
| System.Security.AccessControl/6.0.0 | — |
| System.Security.Cryptography.Cng/5.0.0 | System.Formats.Asn1 |
| System.Security.Cryptography.ProtectedData/6.0.0 | — |
| System.Security.Permissions/6.0.0 | System.Security.AccessControl, System.Windows.Extensions |
| System.Security.Principal.Windows/5.0.0 | — |
| System.Text.Encoding/4.3.0 | Microsoft.NETCore.Platforms, Microsoft.NETCore.Targets, System.Runtime |
| System.Text.Encoding.CodePages/6.0.0 | System.Runtime.CompilerServices.Unsafe |
| System.Text.Encodings.Web/8.0.0 | — |
| System.Text.Json/8.0.4 | System.Text.Encodings.Web |
| System.Threading.Channels/6.0.0 | — |
| System.Threading.Tasks.Extensions/4.5.4 | — |
| System.Windows.Extensions/6.0.0 | System.Drawing.Common |
