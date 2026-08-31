namespace Atlas.Core.Schema;

public static class KubernetesCrdDefinition
{
    public const string ManifestYaml = """
apiVersion: apiextensions.k8s.io/v1
kind: CustomResourceDefinition
metadata:
  name: atlasresources.atlas.io
  annotations:
    controller-gen.kubebuilder.io/version: v0.14.0
spec:
  group: atlas.io
  names:
    kind: AtlasResource
    listKind: AtlasResourceList
    plural: atlasresources
    singular: atlasresource
    shortNames:
      - ar
      - atlas
  scope: Namespaced
  versions:
    - name: v1alpha1
      served: true
      storage: true
      subresources:
        status: {}
      schema:
        openAPIV3Schema:
          type: object
          description: AtlasResource represents a standardized software architecture and catalog specification generated automatically from codebases.
          properties:
            apiVersion:
              type: string
            kind:
              type: string
            metadata:
              type: object
            spec:
              type: object
              required:
                - componentOverview
                - techStack
                - architecture
              properties:
                componentOverview:
                  type: object
                  required:
                    - name
                    - tier
                  properties:
                    name:
                      type: string
                    description:
                      type: string
                    tier:
                      type: string
                    purpose:
                      type: string
                    lifecycle:
                      type: string
                    repositoryUrl:
                      type: string
                    owner:
                      type: string
                techStack:
                  type: object
                  properties:
                    primaryLanguage:
                      type: string
                    languages:
                      type: array
                      items:
                        type: object
                    frameworks:
                      type: array
                      items:
                        type: object
                    runtimes:
                      type: array
                      items:
                        type: object
                    buildSystems:
                      type: array
                      items:
                        type: object
                    packageManagers:
                      type: array
                      items:
                        type: object
                architecture:
                  type: object
                  properties:
                    summary:
                      type: string
                    pattern:
                      type: string
                    components:
                      type: array
                      items:
                        type: object
                    mermaidDiagram:
                      type: string
                apiContracts:
                  type: object
                  properties:
                    endpoints:
                      type: array
                      items:
                        type: object
                    events:
                      type: array
                      items:
                        type: object
                    grpcServices:
                      type: array
                      items:
                        type: object
                dependencies:
                  type: object
                  properties:
                    internalServices:
                      type: array
                      items:
                        type: object
                    externalApis:
                      type: array
                      items:
                        type: object
                    keyPackages:
                      type: array
                      items:
                        type: object
                configuration:
                  type: object
                  properties:
                    environmentVariables:
                      type: array
                      items:
                        type: object
                    configFiles:
                      type: array
                      items:
                        type: object
                dataStores:
                  type: object
                  properties:
                    databases:
                      type: array
                      items:
                        type: object
                    caches:
                      type: array
                      items:
                        type: object
                    messageBrokers:
                      type: array
                      items:
                        type: object
                    objectStorage:
                      type: array
                      items:
                        type: object
                observability:
                  type: object
                  properties:
                    healthChecks:
                      type: array
                      items:
                        type: object
                    logging:
                      type: object
                    metrics:
                      type: object
                    tracing:
                      type: object
""";
}
