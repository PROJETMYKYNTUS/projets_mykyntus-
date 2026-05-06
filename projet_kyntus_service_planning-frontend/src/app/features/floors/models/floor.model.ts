export interface Floor {
  id: number;
  name: string;
  floorNumber: number;       // ← .NET retourne "floorNumber" pas "number"
  description?: string;
  servicesCount: number;     // ← .NET retourne "servicesCount"
}

export interface CreateFloorDto {
  name: string;
  floorNumber: number;       // ← correspondre exactement au .NET
  description?: string;
}

export interface UpdateFloorDto {
  name: string;
  floorNumber: number;       // ← correspondre exactement au .NET
  description?: string;
}