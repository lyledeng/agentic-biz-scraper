/** A single normalized search result from any jurisdiction (v2 unified shape). */
export interface UnifiedSearchResult {
  name: string;
  identifier: string;
  status: string;
  entityType: string | null;
  formationDate: string | null;
  state: string;
  event: string | null;
  uniqueKey: string;
  standingTax: string | null;
  standingRA: string | null;
  registeredOffice: string | null;
}

/** Top-level entity detail response with five nullable sections (v2 unified shape). */
export interface UnifiedEntityDetailResponse {
  details: DetailSection;
  registeredAgent: AgentSection | null;
  certificate: CertificateSection | null;
  parties: PartyEntry[] | null;
  documents: DocumentEntry[] | null;
}

/** Core entity information present for all jurisdictions. */
export interface DetailSection {
  name: string;
  identifier: string;
  status: string;
  formationDate: string | null;
  entityType: string | null;
  jurisdiction: string | null;
  principalAddress: string | null;
  mailingAddress: string | null;
  periodicReportMonth: string | null;
  subStatus: string | null;
  standingTax: string | null;
  standingRA: string | null;
  standingOther: string | null;
  inactiveDate: string | null;
  termOfDuration: string | null;
  formedIn: string | null;
  latestAnnualReportYear: string | null;
  annualReportExempt: string | null;
  licenseTaxPaid: string | null;
  registeredOffice: string | null;
  chapterCode?: string | null;
  certificateNote?: string | null;
  iowaNames?: IowaNameEntry[] | null;
}

/** An entry in the Iowa SOS names list. */
export interface IowaNameEntry {
  name: string;
  type: string;
  status: string;
  modified: boolean;
}

/** Registered agent information. */
export interface AgentSection {
  name: string | null;
  streetAddress: string | null;
  mailingAddress: string | null;
}

/** Certificate of good standing information. */
export interface CertificateSection {
  available: boolean;
  downloads: DownloadReference[] | null;
  error: string | null;
}

/** A party (officer, director, agent) associated with an entity. */
export interface PartyEntry {
  name: string;
  role: string;
  organization: string | null;
  address: string | null;
}

/** A document associated with an entity (WY filing or DE hardcopy). */
export interface DocumentEntry {
  title: string;
  date: string | null;
  downloads: DownloadReference[];
}

/** A single downloadable file within a document entry. */
export interface DownloadReference {
  label: string;
  proxyUrl: string | null;
  fileName: string;
  error: string | null;
}
