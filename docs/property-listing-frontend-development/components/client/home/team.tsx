import Image from 'next/image'
import { LinkedInIcon, SectionTitle, XIcon } from '@/components/shared/brand'
import { team } from '@/lib/data'

export function TeamSection() {
  return (
    <section id="team" className="mx-auto max-w-5xl scroll-mt-24 px-4 py-20 md:px-8 md:py-28">
      <SectionTitle lead="Behind Our" accent="Success" />
      <p className="mx-auto mt-3 max-w-xs text-center text-sm text-muted-foreground">
        Introducing the driving force behind our success — our incredible team members
      </p>
      <ul className="mt-12 grid grid-cols-2 gap-4 md:grid-cols-3 md:gap-6">
        {team.map((m) => (
          <li key={m.name} className="flex flex-col items-center rounded-2xl border border-border bg-surface px-4 py-8 text-center">
            <div className="relative size-16 overflow-hidden rounded-full bg-primary">
              <Image src={m.avatar} alt={m.name} fill sizes="64px" className="object-cover" />
            </div>
            <p className="mt-4 text-sm font-semibold">{m.name}</p>
            <p className="text-xs text-muted-foreground">{m.role}</p>
            <div className="mt-4 flex gap-4">
              <a href="#" aria-label={`${m.name} on X`} className="hover:text-primary">
                <XIcon />
              </a>
              <a href="#" aria-label={`${m.name} on LinkedIn`} className="hover:text-primary">
                <LinkedInIcon />
              </a>
            </div>
          </li>
        ))}
      </ul>
    </section>
  )
}
