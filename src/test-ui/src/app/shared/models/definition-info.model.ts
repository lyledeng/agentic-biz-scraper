/** Metadata for a registered scraping flow definition. */
export interface DefinitionInfo {
  definitionSlug: string;
  name: string;
  description: string | null;
  state: string;
  requiredParameters: ParameterInfo[];
}

/** Parameter descriptor for a flow definition. */
export interface ParameterInfo {
  name: string;
  description: string | null;
}
