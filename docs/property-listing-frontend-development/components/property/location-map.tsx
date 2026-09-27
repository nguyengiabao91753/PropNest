'use client'

import 'leaflet/dist/leaflet.css'
import L from 'leaflet'
import { MapContainer, Marker, TileLayer } from 'react-leaflet'

const icon = L.divIcon({
  className: '',
  iconSize: [40, 40],
  iconAnchor: [20, 20],
  html: '<span style="display:grid;place-items:center;width:40px;height:40px;border-radius:9999px;background:rgba(215,115,12,0.25)"><span style="width:14px;height:14px;border-radius:9999px;background:#d7730c;border:2px solid #fff"></span></span>',
})

export default function LocationMap({ lat, lng }: { lat: number; lng: number }) {
  return (
    <MapContainer center={[lat, lng]} zoom={14} scrollWheelZoom={false} className="size-full">
      <TileLayer url="https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png" attribution="&copy; OpenStreetMap &copy; CARTO" />
      <Marker position={[lat, lng]} icon={icon} />
    </MapContainer>
  )
}
