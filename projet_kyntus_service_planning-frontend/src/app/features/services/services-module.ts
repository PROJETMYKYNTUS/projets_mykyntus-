export interface Service {
  id: number;
  floorId: number;
  floorName: string;
  name: string;
  code: string;
  subServicesCount: number;
  /** Identifiant cellule Prime (Organisation RH). */
  primeCelluleId?: string | null;
}

export interface ServiceDetail {
  id: number;
  floorId: number;
  floorName: string;
  name: string;
  code: string;
  subServices: SubServiceSimple[];  // 🆕 typé
}

// 🆕 AJOUTER
export interface SubServiceSimple {
  id: number;
  serviceId: number;
  serviceName: string;
  name: string;
  code: string;
}

export interface CreateServiceDto {
  floorId: number;
  name: string;
  code: string;
}

export interface UpdateServiceDto {
  floorId: number;
  name: string;
  code: string;
}