import { SiteHeader } from '@/components/client/site-header'
import { SiteFooter } from '@/components/client/site-footer'
import { HomeHero } from '@/components/client/home/hero'
import { HomeAbout } from '@/components/client/home/about'
import { LatestProjects } from '@/components/client/home/latest-projects'
import { ProjectCategories } from '@/components/client/home/categories'
import { WhatWeOffer } from '@/components/client/home/offer'
import { TeamSection } from '@/components/client/home/team'
import { FaqSection } from '@/components/client/home/faq'
import { Testimonials } from '@/components/client/home/testimonials'

export default function HomePage() {
  return (
    <>
      <SiteHeader variant="centered" />
      <main>
        <HomeHero />
        <HomeAbout />
        <LatestProjects />
        <ProjectCategories />
        <WhatWeOffer />
        <TeamSection />
        <FaqSection />
        <Testimonials />
      </main>
      <SiteFooter />
    </>
  )
}
