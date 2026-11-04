import {
  BarChart3,
  ClipboardList,
  LayoutDashboard,
  MapPin,
  Route,
  Truck,
  Users,
  Wrench,
  type LucideIcon,
} from 'lucide-react'

export interface ModuleDefinition {
  key: string
  title: string
  path: string
  icon: LucideIcon
  description: string
  /** Placeholder modules show a "not yet implemented" page. */
  implemented: boolean
  /** Planned capabilities listed on the placeholder page (informational only). */
  planned?: string[]
}

export const modules: ModuleDefinition[] = [
  {
    key: 'dashboard',
    title: 'Dashboard',
    path: '/dashboard',
    icon: LayoutDashboard,
    description: 'Operational overview of orders, assignments and fleet status.',
    implemented: true,
  },
  {
    key: 'dispatch',
    title: 'Dispatch Management',
    path: '/dispatch',
    icon: Route,
    description: 'Assign drivers and vehicles to transport orders and manage dispatch assignments.',
    implemented: true,
  },
  {
    key: 'orders',
    title: 'Transport Orders',
    path: '/orders',
    icon: ClipboardList,
    description: 'Capture, edit and track customer transport orders from booking through to invoicing.',
    implemented: false,
    planned: ['Order intake and editing', 'Customer account management', 'Order status workflow', 'Order documents'],
  },
  {
    key: 'fleet',
    title: 'Fleet Management',
    path: '/fleet',
    icon: Truck,
    description: 'Maintain the vehicle register, registrations, certifications and vehicle status.',
    implemented: false,
    planned: ['Vehicle register', 'Registration and COF expiry tracking', 'Vehicle status changes', 'Utilisation history'],
  },
  {
    key: 'drivers',
    title: 'Driver Management',
    path: '/drivers',
    icon: Users,
    description: 'Manage driver records, licences, employment status and rosters.',
    implemented: false,
    planned: ['Driver records', 'Licence and endorsement tracking', 'Leave and rostering', 'Employment status changes'],
  },
  {
    key: 'tracking',
    title: 'Delivery Tracking',
    path: '/tracking',
    icon: MapPin,
    description: 'Follow jobs from pickup to delivery, including proof of delivery.',
    implemented: false,
    planned: ['Job progress milestones', 'Proof of delivery', 'Customer notifications', 'Exception reporting'],
  },
  {
    key: 'maintenance',
    title: 'Maintenance',
    path: '/maintenance',
    icon: Wrench,
    description: 'Schedule servicing and record repairs for fleet vehicles.',
    implemented: false,
    planned: ['Service schedules', 'Workshop job cards', 'Defect reporting', 'Maintenance history'],
  },
  {
    key: 'reports',
    title: 'Reports & Analytics',
    path: '/reports',
    icon: BarChart3,
    description: 'Operational and financial reporting across orders, fleet and drivers.',
    implemented: false,
    planned: ['On-time delivery reporting', 'Fleet utilisation', 'Driver activity', 'Data export'],
  },
]
