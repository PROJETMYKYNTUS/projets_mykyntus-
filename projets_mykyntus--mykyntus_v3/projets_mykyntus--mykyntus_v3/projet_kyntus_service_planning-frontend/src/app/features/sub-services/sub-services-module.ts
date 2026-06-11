export interface SubService {
  id: number;
  serviceId: number;
  serviceName: string;
  name: string;
  code: string;
  employeesCount: number;
}

export interface SubServiceDetail {
  id: number;
  serviceId: number;
  serviceName: string;
  floorName: string;
  name: string;
  code: string;
  employees: UserSimple[];
}

export interface UserSimple {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  roleName: string;
}

export interface CreateSubServiceDto {
  serviceId: number;
  name: string;
  code: string;
}

export interface UpdateSubServiceDto {
  serviceId: number;
  name: string;
  code: string;
}