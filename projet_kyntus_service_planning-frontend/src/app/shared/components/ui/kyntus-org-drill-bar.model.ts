export interface KyntusDrillLevel {
  key: string;
  placeholder: string;
  options: { value: string; label: string }[];
  value: string;
  disabled?: boolean;
}
