'use client'

import 'leaflet/dist/leaflet.css'
import L from 'leaflet'
import Link from 'next/link'
import { useEffect, useMemo, useState } from 'react'
import { MapContainer, Marker, Popup, TileLayer, useMap } from 'react-leaflet'
import { Minus, Plus } from 'lucide-react'
import type { Property } from '@/lib/data'
import { formatCompactPrice } from '@/lib/format'
import { cn } from '@/lib/utils'

const tiles = {
  map: {
    url: 'https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png',
    attribution: '&copy; OpenStreetMap &copy; CARTO',
  },
  satellite: {
    url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
    attribution: 'Tiles &copy; Esri',
  },
}

function pin(active: boolean) {
  return L.divIcon({
    className: '',
    iconSize: [36, 36],
    iconAnchor: [18, 18],
    html: `<span style="display:grid;place-items:center;width:36px;height:36px;border-radius:9999px;background:${
      active ? 'rgba(215,115,12,0.45)' : 'rgba(215,115,12,0.2)'
    };transition:background .2s"><span style="width:12px;height:12px;border-radius:9999px;background:#d7730c;border:2px solid #fff"></span></span>`,
  })
}

function FitBounds({ properties }: { properties: Property[] }) {
  const map = useMap()
  useEffect(() => {
    if (!properties.length) return
    map.fitBounds(L.latLngBounds(properties.map((p) => [p.lat, p.lng])), { padding: [40, 40], maxZoom: 13 })
  }, [map, properties])
  return null
}

function ZoomControls() {
  const map = useMap()
  return (
    <div className="absolute bottom-6 right-6 z-[500] flex flex-col overflow-hidden rounded-lg bg-card shadow-md">
      <button type="button" aria-label="Zoom in" onClick={() => map.zoomIn()} className="grid size-11 place-items-center hover:text-primary">
        <Plus className="size-5" />
      </button>
      <button type="button" aria-label="Zoom out" onClick={() => map.zoomOut()} className="grid size-11 place-items-center border-t border-border hover:text-primary">
        <Minus className="size-5" />
      </button>
    </div>
  )
}

export default function ListingsMap({ properties, activeId }: { properties: Property[]; activeId: string | null }) {
  const [mode, setMode] = useState<keyof typeof tiles>('map')
  const icons = useMemo(() => ({ on: pin(true), off: pin(false) }), [])

  return (
    <div className="relative size-full overflow-hidden rounded-2xl border border-border">
      <MapContainer center={[10.7769, 106.7009]} zoom={12} zoomControl={false} scrollWheelZoom className="size-full">
        <TileLayer key={mode} url={tiles[mode].url} attribution={tiles[mode].attribution} />
        <FitBounds properties={properties} />
        {properties.map((p) => (
          <Marker key={p.id} position={[p.lat, p.lng]} icon={p.id === activeId ? icons.on : icons.off} zIndexOffset={p.id === activeId ? 1000 : 0}>
            <Popup>
              <Link href={`/properties/${p.id}`} className="block font-sans text-sm">
                <strong className="block line-clamp-1">{p.title}</strong>
                <span className="text-primary font-bold">{formatCompactPrice(p)}</span> · {p.district || p.suburb}
              </Link>
            </Popup>
          </Marker>
        ))}
        <ZoomControls />
      </MapContainer>
      <div className="absolute bottom-6 left-6 z-[500] flex overflow-hidden rounded-md bg-card shadow-md" role="group" aria-label="Map style">
        {(['map', 'satellite'] as const).map((m) => (
          <button
            key={m}
            type="button"
            aria-pressed={mode === m}
            onClick={() => setMode(m)}
            className={cn('px-5 py-2.5 text-sm capitalize transition-colors', mode === m ? 'bg-primary text-primary-foreground' : 'hover:text-primary')}
          >
            {m}
          </button>
        ))}
      </div>
    </div>
  )
}
