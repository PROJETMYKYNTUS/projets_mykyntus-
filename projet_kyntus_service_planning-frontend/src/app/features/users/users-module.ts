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
  orgPoleName?: string | null;
  orgCelluleName?: string | null;
  orgServiceName?: string | null;
  orgOperationalDepartmentName?: string | null;
  managedSubServices: SubServiceSimple[];
  managedServices: ServiceSimple[];   // 🆕
  firstName: string;
  lastName: string;
  email: string;
  hireDate: string;
  isActive: boolean;
  createdAt: string;
  level: number;
  customFields?: Record<string, string | null>;
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
  customFields?: Record<string, string | null>;
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
  customFields?: Record<string, string | null>;
}