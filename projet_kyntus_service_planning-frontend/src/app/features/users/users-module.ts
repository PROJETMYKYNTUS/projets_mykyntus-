export interface SubServiceSimple {
  id: number;
  name: string;
  serviceName: string;
}

// 🆕 AJOUTER
export interface ServiceSimple {
  id: number;
  name: string;
  floorName: string;
}

export interface User {
  id: number;
  guid: string;
  roleId: number;
  roleName: string;
  subServiceId?: number;
  subServiceName?: string;
  managedSubServices: SubServiceSimple[];
  managedServices: ServiceSimple[];   // 🆕
  firstName: string;
  lastName: string;
  email: string;
  hireDate: string;
  isActive: boolean;
  createdAt: string;
  level: number;
}

export interface CreateUserDto {
  roleId: number;
  subServiceId?: number;
  managedSubServiceIds: number[];
  managedServiceIds: number[];        // 🆕
  firstName: string;
  lastName: string;
  hireDate: string;
  email: string;
  level: number;
}

export interface UpdateUserDto {
  roleId: number;
  subServiceId?: number;
  managedSubServiceIds: number[];
  managedServiceIds: number[];        // 🆕
  firstName: string;
  lastName: string;
  hireDate: string;
  email: string;
  isActive: boolean;
  level: number;
}