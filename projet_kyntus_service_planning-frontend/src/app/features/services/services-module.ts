export interface Service {
  id: number;
  floorId: number;
  floorName: string;
  name: string;
  code: string;
  subServicesCount: number;
}

export interface ServiceDetail {
  id: number;
  floorId: number;
  floorName: string;
  name: string;
  code: string;
  subServices: any[];
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