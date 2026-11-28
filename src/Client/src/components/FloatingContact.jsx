import { Phone } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useLocation } from 'react-router'
import { CONTACT_PHONE } from '@/lib/brand'
import { formatPhone, phoneHref, whatsappHref } from '@/lib/format'
import { cn } from '@/lib/utils'

// WhatsApp glyph from Simple Icons (CC0).
function WhatsAppIcon(props) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" {...props}>
      <path d="M17.472 14.382c-.297-.149-1.758-.867-2.03-.967-.273-.099-.471-.148-.67.15-.197.297-.767.966-.94 1.164-.173.199-.347.223-.644.075-.297-.15-1.255-.463-2.39-1.475-.883-.788-1.48-1.761-1.653-2.059-.173-.297-.018-.458.13-.606.134-.133.298-.347.446-.52.149-.174.198-.298.298-.497.099-.198.05-.371-.025-.52-.075-.149-.669-1.612-.916-2.207-.242-.579-.487-.5-.669-.51-.173-.008-.371-.01-.57-.01-.198 0-.52.074-.792.372-.272.297-1.04 1.016-1.04 2.479 0 1.462 1.065 2.875 1.213 3.074.149.198 2.096 3.2 5.077 4.487.709.306 1.262.489 1.694.625.712.227 1.36.195 1.871.118.571-.085 1.758-.719 2.006-1.413.248-.694.248-1.289.173-1.413-.074-.124-.272-.198-.57-.347m-5.421 7.403h-.004a9.87 9.87 0 01-5.031-1.378l-.361-.214-3.741.982.998-3.648-.235-.374a9.86 9.86 0 01-1.51-5.26c.001-5.45 4.436-9.884 9.888-9.884 2.64 0 5.122 1.03 6.988 2.898a9.825 9.825 0 012.893 6.994c-.003 5.45-4.437 9.884-9.885 9.884m8.413-18.297A11.815 11.815 0 0012.05 0C5.495 0 .16 5.335.157 11.892c0 2.096.547 4.142 1.588 5.945L.057 24l6.305-1.654a11.882 11.882 0 005.683 1.448h.005c6.554 0 11.89-5.335 11.893-11.893a11.821 11.821 0 00-3.48-8.413Z" />
    </svg>
  )
}

// Pages for partners and admins don't need the customer contact buttons.
const hiddenOn = /^\/(admin|partner)(\/|$)/

/**
 * Call and WhatsApp buttons pinned to the bottom-right corner. They appear only once the page's own contact
 * buttons (marked `data-contact-cta`, e.g. in the home hero) have scrolled out of view, or, on pages without
 * any, after scrolling down a little. An IntersectionObserver does the watching, so there is no scroll work.
 */
export default function FloatingContact() {
  const { pathname } = useLocation()
  const [visible, setVisible] = useState(false)
  const disabled = hiddenOn.test(pathname)

  useEffect(() => {
    if (disabled || typeof IntersectionObserver === 'undefined') return undefined
    // The target is rendered by the page; look it up after the route has rendered.
    const anchor = document.querySelector('[data-contact-cta]')
    let sentinel = null
    let target = anchor
    if (!target) {
      // No contact buttons on this page: show once the top 400px have scrolled away.
      sentinel = document.createElement('div')
      sentinel.setAttribute('aria-hidden', 'true')
      sentinel.style.cssText = 'position:absolute;top:0;left:0;width:1px;height:400px;pointer-events:none;'
      document.body.appendChild(sentinel)
      target = sentinel
    }
    const observer = new IntersectionObserver(([entry]) => setVisible(!entry.isIntersecting))
    observer.observe(target)
    return () => {
      observer.disconnect()
      sentinel?.remove()
      setVisible(false)
    }
  }, [pathname, disabled])

  if (disabled) return null

  const button =
    'grid size-14 place-items-center rounded-full text-white shadow-lg ring-4 ring-white/70 transition-transform duration-200 hover:scale-105 focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-foreground active:scale-95'

  return (
    <div
      // inert keeps the hidden buttons out of the tab order and away from screen readers.
      inert={!visible}
      className={cn(
        'fixed right-4 z-50 flex flex-col items-end gap-3 transition-[opacity,translate] duration-300 motion-reduce:transition-none',
        'bottom-[max(1rem,env(safe-area-inset-bottom))]',
        visible ? 'translate-y-0 opacity-100' : 'pointer-events-none translate-y-4 opacity-0',
      )}
    >
      {/* The number sits above the icon (inside the same link) so callers know who they are calling. */}
      <a
        href={phoneHref(CONTACT_PHONE)}
        aria-label={`Call us on ${formatPhone(CONTACT_PHONE)}`}
        className="group flex flex-col items-end gap-1.5 rounded-full focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-foreground"
      >
        <span
          aria-hidden="true"
          className="rounded-full bg-card px-2.5 py-1 text-xs font-semibold whitespace-nowrap text-foreground tabular-nums shadow-md ring-1 ring-foreground/10"
        >
          {formatPhone(CONTACT_PHONE)}
        </span>
        <span className={cn(button, 'bg-brand-blue group-hover:scale-105 group-active:scale-95')}>
          <Phone className="size-6" aria-hidden="true" />
        </span>
      </a>
      <a
        href={whatsappHref(CONTACT_PHONE)}
        target="_blank"
        rel="noreferrer"
        aria-label="Chat with us on WhatsApp (opens in a new tab)"
        // WhatsApp green darkened to keep 3:1 contrast against the white glyph.
        className={cn(button, 'bg-[#128c4a]')}
      >
        <WhatsAppIcon className="size-7" />
      </a>
    </div>
  )
}
