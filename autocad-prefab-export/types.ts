/**
 * Eleman Kimlik ve İsimlendirme sistemi — çekirdek tip tanımları.
 *
 * Üç katmanlı mimari:
 *  1) Source Identity (IFC GUID)  → `sourceGuid`
 *  2) Product Type Code (Catalog) → `TYPOLOGY-FAMILY-VARIANT`
 *  3) Instance Mark (Project)      → çözümlenmiş benzersiz isim
 *
 * Prompt paketi: frontend/promts/23-element-kimlik-ve-isimlendirme
 */

export type ElementCategory =
  | 'superstructure'
  | 'substructure'
  | 'industrial'
  | 'architectural'
  /** Çevre koruma (gürültü / istinat vb.) */
  | 'environmental_protection'
  /** Çevre düzenleme (bordür, parke elemanı vb.) */
  | 'landscaping'
  /** Enerji nakil (kablo kanalı, hat döşemesi vb.) */
  | 'energy_carrier'
  /** Özel imalat */
  | 'custom_prefab'

export type SourceSystem =
  | 'TEKLA'
  | 'REVIT'
  | 'ALLPLAN'
  | 'AUTOCAD'
  | 'MANUAL'
  | 'IFC_GENERIC'

export type IfcClassName =
  | 'IfcColumn'
  | 'IfcBeam'
  | 'IfcSlab'
  | 'IfcWall'
  | 'IfcStairFlight'
  | 'IfcFooting'
  | 'IfcMember'
  | 'IfcPlate'
  | 'IfcCovering'
  | 'IfcPile'
  | 'IfcPipeSegment'
  | 'IfcCableCarrierSegment'
  | 'IfcBuildingElementProxy'

/** IFC 4.3.2 spec'teki (gerekli alt küme) predefined type union */
export type IfcPredefinedType =
  // Column
  | 'COLUMN'
  | 'PILASTER'
  // Beam
  | 'BEAM'
  | 'T_BEAM'
  | 'EDGEBEAM'
  | 'SPANDREL'
  | 'PIERCAP'
  | 'LINTEL'
  | 'GIRDER_SEGMENT'
  | 'HOLLOWCORE'
  | 'CORNICE'
  | 'DIAPHRAGM'
  | 'HATSTONE'
  | 'JOIST'
  // Slab
  | 'FLOOR'
  | 'ROOF'
  | 'LANDING'
  | 'BASESLAB'
  // Wall
  | 'SOLIDWALL'
  | 'SHEAR'
  | 'PARTITIONING'
  | 'PARAPET'
  | 'RETAININGWALL'
  // Stair
  | 'STRAIGHT'
  | 'WINDER'
  | 'HALF_TURN_STAIR'
  | 'SPIRAL_STAIR'
  // Footing
  | 'PAD_FOOTING'
  | 'STRIP_FOOTING'
  | 'PILE_CAP'
  // Member
  | 'BRACE'
  | 'CHORD'
  // Generic
  | 'USERDEFINED'
  | 'NOTDEFINED'

export type UnitSystem = 'metric' | 'imperial' | 'mixed'

export type DimensionUnit = 'mm' | 'cm' | 'm' | 'in' | 'ft'

// ============================================================================
// 1. SISTEM KATALOG (immutable)
// ============================================================================

export type GeometryHint = {
  type: string
  axis?: 'X' | 'Y' | 'Z'
  section?: string
  layers?: string[]
  stepProfile?: string
  standardWidth?: number
}

export type ElementTypeCatalogEntry = {
  id: string
  nameTr: string
  nameEn: string
  defaultCode: string
  ifcClass: IfcClassName
  defaultIfcPredefinedType?: IfcPredefinedType
  category: ElementCategory
  order: number
  description?: string
  allowedTypologies: string[]
}

export type Typology = {
  id: string
  elementTypeId: string
  nameTr: string
  nameEn: string
  defaultCode: string
  ifcPredefinedType?: IfcPredefinedType
  ifcObjectType?: string
  identifyingDimensions: string[]
  defaultSizeFormatId: string
  geometryHint?: GeometryHint
  notes?: string
}

export type IdentifyingDimension = {
  id: string
  nameTr: string
  nameEn: string
  unit: DimensionUnit
  required: boolean
  description?: string
}

export type SizeFormat = {
  id: string
  nameTr: string
  nameEn: string
  inputs: string[]
  outputTemplate: string
  unitSystem: UnitSystem
  separator?: string
  examples?: Array<{ input: Record<string, number>; output: string }>
}

export type IfcMappingRule = {
  id: string
  ifcClass: IfcClassName
  ifcPredefinedType?: IfcPredefinedType
  ifcObjectType?: string
  heuristic?: string
  systemElementTypeId: string
  systemTypologyId?: string
  priority: number
  notes?: string
}

// ============================================================================
// 2. FIRMA KATMANI (localStorage'da saklanır)
// ============================================================================

export type FirmCodeOverrideScope =
  | 'element_type'
  | 'typology'
  | 'size_format'
  | 'separator'

export type FirmCodeOverride = {
  id: string
  firmId: string
  scope: FirmCodeOverrideScope
  /** scope'a göre: ElementType.id | Typology.id | Typology.id (size_format) | '__template__' */
  refId: string
  customCode?: string
  customSizeFormatId?: string
  active: boolean
  createdAt: string
  updatedAt: string
  notes?: string
}

export type NamingToken =
  | 'FIRM_CODE'
  | 'PROJECT_CODE'
  | 'TYPOLOGY_CODE'
  | 'FAMILY_CODE'
  | 'VARIANT_CODE'
  | 'SIZE'
  | 'SEQUENCE'
  | 'REVISION'

export type NamingTokenConfig = {
  token: NamingToken
  enabled: boolean
  order: number
  prefix?: string
  suffix?: string
  formatId?: string
  padding?: number
}

export type FirmNamingTemplate = {
  id: string
  firmId: string
  name: string
  tokens: NamingTokenConfig[]
  separator: string
  sizeConcat: boolean
  sequencePadding: number
  revisionPrefix: string
  revisionZeroSuffix: boolean
  uppercaseEnforce: boolean
  createdAt: string
  updatedAt: string
}

export type FirmProfile = {
  id: string
  name: string
  slug: string
  unitSystem: UnitSystem
  firmCodePrefix: string
  defaultTemplateId: string
  active: boolean
  createdAt: string
  updatedAt: string
}

// ============================================================================
// 3. PROJE KATMANI
// ============================================================================

export type ProjectElementStatus = 'draft' | 'active' | 'archived'

export type ProjectElement = {
  id: string
  projectId: string
  firmId: string
  elementTypeId: string
  typologyId: string
  variantCode?: string
  sourceSystem?: SourceSystem
  sourceGuid?: string
  sourceName?: string
  sourceFile?: string
  dimensions: Record<string, number>
  sequence: number
  revision: number
  instanceMark: string
  attributes: Record<string, unknown>
  status: ProjectElementStatus
  namingTemplateId: string
  importDate?: string
  createdAt: string
  updatedAt: string
}

export type ProjectSequenceCounter = {
  id: string
  projectId: string
  scope: 'element_type' | 'typology' | 'type_and_size'
  scopeKey: string
  currentValue: number
}

export type ProjectLike = {
  id: string
  code: string
  name?: string
}

export type ProjectProductSource = 'MANUAL' | 'IFC' | 'CAD' | 'STANDARD_LIBRARY'

export type ProductLifecycleStatus = 'tasarim' | 'uretim' | 'saha' | 'montaj' | 'tamamlandi'

/** Donatı şekil sınıflandırması (BS 8666 / prefabrik pratik) */
export type RebarShapeType =
  | 'straight'
  | 'stirrup'
  | 'hook'
  | 'l_bar'
  | 'u_bar'
  | 'crank'
  | 'custom'

export type ProductMaterialEntry = {
  id: string
  /** Malzeme kataloğu tanımı (opsiyonel) */
  materialDefId?: string
  category: string
  name: string
  specification: string
  quantity: number
  unit: string
}

export type ProductRebarEntry = {
  id: string
  position: string
  diameterMm: number
  steelGrade: string
  shape: RebarShapeType
  developedLengthMm: number
  count: number
  totalWeightKg: number
  notes?: string
}

export type ProductRebarSummary = {
  totalWeightKg: number
  straightBarCount: number
  shapedBarCount: number
  totalDevelopedLengthM: number
}

export type ProductDrawingRevision = {
  id: string
  revision: string
  title: string
  updatedAt: string
  updatedBy: string
  changeNote: string
  pdfUrl?: string
  fileName?: string
}

export type ProductActivity = {
  id: string
  at: string
  text: string
  by?: string
}

/** Seri şablonunda / projede assembly satırı (MVP: metin + miktar) */
export type AssemblyComponentLine = {
  id: string
  label: string
  quantity: number
  unit: string
  notes?: string
}

export type ProjectProduct = {
  id: string
  projectId: string
  name: string
  code: string
  elementTypeId?: string
  typologyId?: string
  source: ProjectProductSource
  revision: number
  status: 'active' | 'superseded'
  note?: string
  createdAt: string
  /** Uzun teknik tanım */
  definition?: string
  lifecycleStatus?: ProductLifecycleStatus
  volumeM3?: number
  /** Tipoloji identifyingDimensions anahtarları → mm veya adet */
  dimensions?: Record<string, number>
  materials?: ProductMaterialEntry[]
  rebarSchedule?: ProductRebarEntry[]
  rebarSummary?: ProductRebarSummary
  drawingRevisions?: ProductDrawingRevision[]
  activities?: ProductActivity[]
  /** Şablondan gelen assembly satırları (seri kütüphanesi) */
  assemblyComponents?: AssemblyComponentLine[]
}

/**
 * Firma düzeyinde seri üretim ürün şablonu — projeden bağımsız;
 * `ProjectProduct` ile uyumlu teknik alanlar ürün sekmelerinde yeniden kullanılır.
 */
export type StandardSeriesTemplate = {
  id: string
  firmId: string
  code: string
  name: string
  active: boolean
  description?: string
  tags?: string[]
  elementTypeId?: string
  typologyId?: string
  note?: string
  definition?: string
  lifecycleStatus?: ProductLifecycleStatus
  volumeM3?: number
  dimensions?: Record<string, number>
  materials?: ProductMaterialEntry[]
  rebarSchedule?: ProductRebarEntry[]
  rebarSummary?: ProductRebarSummary
  drawingRevisions?: ProductDrawingRevision[]
  activities?: ProductActivity[]
  assemblyComponents?: AssemblyComponentLine[]
  updatedAt?: string
}

// ============================================================================
// 4. YARDIMCI / DTO
// ============================================================================

export type ResolveContext = {
  firm: FirmProfile
  template: FirmNamingTemplate
  project: ProjectLike
  overrides: FirmCodeOverride[]
}

export type NameResolutionTrace = {
  token: NamingToken
  rawValue: string | number | null
  formatted: string | null
  source: 'system' | 'firm-override' | 'project-override' | 'dimension' | 'sequence' | 'revision'
  skipped?: boolean
  reason?: string
}

export type NameResolutionResult = {
  instanceMark: string
  trace: NameResolutionTrace[]
  warnings: string[]
}
