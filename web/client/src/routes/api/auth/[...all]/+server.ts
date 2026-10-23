import type { RequestHandler } from './$types';
import { getAuth } from '$lib/server/auth';

export const GET: RequestHandler = ({ request }) => getAuth().handler(request);

export const POST: RequestHandler = ({ request }) => getAuth().handler(request);

export const PUT: RequestHandler = ({ request }) => getAuth().handler(request);

export const PATCH: RequestHandler = ({ request }) => getAuth().handler(request);

export const DELETE: RequestHandler = ({ request }) => getAuth().handler(request);
